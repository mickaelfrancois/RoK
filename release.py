#!/usr/bin/env python3
"""
Release automation script for Rok.

Usage:
    python release.py <version> [platform] [--dryrun]

Arguments:
    version   Version number in a.b.c.d format (e.g. 1.8.0.0)
    platform  Target platform: x86 | x64 | ARM64  (default: x64)
    --dryrun  Run all steps locally without pushing the branch or tag to GitHub

Steps performed:
    1. Creates branch release/<version>
    2. Updates the version in Presentation/Package.appxmanifest
    3. Commits the version bump
    4. Builds the sideload MSIX package via MSBuild (Release mode)
    5. Creates annotated tag v<version>
    6. Pushes the branch and tag to origin (skipped with --dryrun)
    7. Optionally submits to the Microsoft Store via msstore-cli (with confirmation)

Output packages are placed in:
    Presentation/AppPackages/Rok_<version>_<platform>_Test/  (sideload)
    Presentation/AppPackages/Rok_<version>_<platform>.msixupload  (store)

Examples:
    python release.py 1.8.0.0
    python release.py 1.8.0.0 ARM64
    python release.py 1.8.0.0 x64 --dryrun

Prerequisites:
    - Run from a Visual Studio Developer Command Prompt (msbuild must be in PATH)
    - Working tree must be clean before running
    - For Store submission: msstore-cli installed and configured
        winget install --id 9P53PC5S0PHJ --source msstore
        msstore configure
"""

import re
import subprocess
import sys
from pathlib import Path

MANIFEST_PATH = Path("Presentation/Package.appxmanifest")
PROJECT_PATH  = Path("Presentation/Rok.csproj")
PLATFORMS     = ["x86", "x64", "ARM64"]
STORE_APP_ID  = "9NX19R28Q92S"  # Partner Center: Apps and games -> your app -> App identity -> Store ID


def run(args: list[str]) -> None:
    print(f"\n>> {' '.join(args)}")
    result = subprocess.run(args, text=True)
    if result.returncode != 0:
        print(f"Error: command failed with exit code {result.returncode}")
        sys.exit(result.returncode)


def validate_version(version: str) -> None:
    if not re.fullmatch(r"\d+\.\d+\.\d+\.\d+", version):
        print(f"Error: version must follow a.b.c.d format, got '{version}'")
        sys.exit(1)


def check_clean_working_tree() -> None:
    result = subprocess.run(
        ["git", "status", "--porcelain"], capture_output=True, text=True
    )
    if result.stdout.strip():
        print("Error: working tree is not clean. Commit or stash changes first.")
        sys.exit(1)


def current_branch() -> str:
    result = subprocess.run(
        ["git", "rev-parse", "--abbrev-ref", "HEAD"],
        capture_output=True, text=True, check=True
    )
    return result.stdout.strip()


def branch_exists(branch: str) -> bool:
    result = subprocess.run(
        ["git", "rev-parse", "--verify", branch],
        capture_output=True, text=True
    )
    return result.returncode == 0


def tag_exists(tag: str) -> bool:
    result = subprocess.run(
        ["git", "tag", "-l", tag],
        capture_output=True, text=True
    )
    return bool(result.stdout.strip())


def update_manifest(version: str) -> bool:
    content = MANIFEST_PATH.read_text(encoding="utf-8")
    if not re.search(r'<Identity[^>]*?Version="[^"]*"', content, flags=re.DOTALL):
        print(f"Error: Version attribute not found in {MANIFEST_PATH}")
        sys.exit(1)
    updated = re.sub(
        r'(<Identity[^>]*?Version=")[^"]*(")',
        rf'\g<1>{version}\g<2>',
        content,
        count=1,
        flags=re.DOTALL,
    )
    if updated == content:
        print(f"Manifest version already at {version}, skipping update")
        return False
    MANIFEST_PATH.write_text(updated, encoding="utf-8")
    print(f"Manifest version updated to {version}")
    return True


PLATFORM_RID = {"x86": "win-x86", "x64": "win-x64", "ARM64": "win-arm64"}


def publish(platform: str) -> None:
    run([
        "msbuild", str(PROJECT_PATH),
        "/t:Build",
        "/p:Configuration=Release",
        f"/p:Platform={platform}",
        f"/p:RuntimeIdentifier={PLATFORM_RID[platform]}",
        "/p:AppxBundle=Never",
        "/p:UapAppxPackageBuildMode=SideloadOnly",
        "/p:AppxPackageSigningEnabled=false",
        "/p:GenerateAppxPackageOnBuild=true",
    ])


def publish_store(platform: str) -> Path:
    run([
        "msbuild", str(PROJECT_PATH),
        "/t:Build",
        "/p:Configuration=Release",
        f"/p:Platform={platform}",
        f"/p:RuntimeIdentifier={PLATFORM_RID[platform]}",
        "/p:AppxBundle=Never",
        "/p:UapAppxPackageBuildMode=StoreUpload",
        "/p:AppxPackageSigningEnabled=false",
        "/p:GenerateAppxPackageOnBuild=true",
    ])
    packages_dir = Path("Presentation") / "AppPackages"
    matches = list(packages_dir.glob(f"Rok_*_{platform}.msixupload"))
    if not matches:
        print(f"Error: no .msixupload file found in {packages_dir}")
        sys.exit(1)
    return max(matches, key=lambda p: p.stat().st_mtime)


def check_msstore_cli() -> None:
    result = subprocess.run(["msstore", "--version"], capture_output=True, text=True)
    if result.returncode != 0:
        print("Error: msstore-cli not found. Install it with:")
        print("  winget install --id 9P53PC5S0PHJ --source msstore")
        print("Then configure it once with: msstore configure")
        sys.exit(1)


def main() -> None:
    args = sys.argv[1:]
    dry_run = "--dryrun" in args
    args = [a for a in args if a != "--dryrun"]

    if not args:
        print("Usage:   python release.py <version> [platform] [--dryrun]")
        print("Version: a.b.c.d              (ex: 1.8.0.0)")
        print(f"Platform: {' | '.join(PLATFORMS)}  (default: x64)")
        print("--dryrun: build locally without pushing to GitHub")
        sys.exit(1)

    version  = args[0]
    platform = args[1] if len(args) > 1 else "x64"

    validate_version(version)

    if platform not in PLATFORMS:
        print(f"Error: platform must be one of {PLATFORMS}")
        sys.exit(1)

    branch  = f"release/{version}"
    tag     = f"v{version}"
    out_dir = Path("Presentation") / "AppPackages" / f"Rok_{version}_{platform}_Test"

    if dry_run:
        print("[dry-run] No push will be performed")

    # 1. Create or reuse
    if current_branch() == branch:
        print(f"Already on branch {branch}, skipping branch creation")
    elif branch_exists(branch):
        print(f"Error: branch {branch} already exists but current branch is '{current_branch()}'")
        sys.exit(1)
    else:
        check_clean_working_tree()
        run(["git", "checkout", "-b", branch])

    # 2. Update Package.appxmanifest and commit if changed
    if update_manifest(version):
        run(["git", "add", str(MANIFEST_PATH)])
        run(["git", "commit", "-m", f"chore: bump version to {version}"])
    else:
        print("Skipping commit, manifest was already up to date")

    # 4. Build and publish the MSIX package
    publish(platform)

    # 5. Create an annotated tag
    if tag_exists(tag):
        print(f"Tag {tag} already exists, skipping")
    else:
        run(["git", "tag", "-a", tag, "-m", f"Release {version}"])

    # 6. Push branch and tag (skipped in dry-run)
    if dry_run:
        print(f"\n[dry-run] Skipped: git push origin {branch}")
        print(f"[dry-run] Skipped: git push origin {tag}")
    else:
        run(["git", "push", "origin", branch])
        run(["git", "push", "origin", tag])

    print(f"\nRelease {tag} created successfully -> origin/{branch}")
    print(f"Packages available in: {out_dir}")

    # 7. Optional Store submission
    answer = input("\nSubmit to Microsoft Store? [y/N] ").strip().lower()
    if answer == "y":
        check_msstore_cli()
        msixupload = publish_store(platform)
        print(f"\nStore package ready: {msixupload.resolve()}")
        print(f"\nUpdate your release notes before confirming:")
        print(f"  https://partner.microsoft.com/dashboard/products/{STORE_APP_ID}/submissions")
        confirm = input("Release notes updated? Confirm submission [y/N] ").strip().lower()
        if confirm == "y":
            if dry_run:
                print(f"[dry-run] Skipped: msstore publish {msixupload} --appId {STORE_APP_ID}")
            else:
                run(["msstore", "publish", str(msixupload), "--appId", STORE_APP_ID])
                print(f"Store submission initiated for {msixupload.name}")
        else:
            print(f"Submission cancelled. Package available at: {msixupload.resolve()}")


if __name__ == "__main__":
    main()