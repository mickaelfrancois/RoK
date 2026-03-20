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
    4. Builds and publishes the MSIX package via MSBuild (Release mode)
    5. Creates annotated tag v<version>
    6. Pushes the branch and tag to origin (skipped with --dryrun)

Output packages are placed in:
    Presentation/AppPackages/Rok_<version>_<platform>_Test/

Examples:
    python release.py 1.8.0.0
    python release.py 1.8.0.0 ARM64
    python release.py 1.8.0.0 x64 --dryrun

Prerequisites:
    - Run from a Visual Studio Developer Command Prompt (msbuild must be in PATH)
    - Working tree must be clean before running
"""

import re
import subprocess
import sys
from pathlib import Path

MANIFEST_PATH = Path("Presentation/Package.appxmanifest")
PROJECT_PATH  = Path("Presentation/Rok.csproj")
PLATFORMS     = ["x86", "x64", "ARM64"]


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


def update_manifest(version: str) -> None:
    content = MANIFEST_PATH.read_text(encoding="utf-8")
    updated = re.sub(
        r'(<Identity[^>]*?Version=")[^"]*(")',
        rf'\g<1>{version}\g<2>',
        content,
        count=1,
        flags=re.DOTALL,
    )
    if updated == content:
        print(f"Error: Version attribute not found in {MANIFEST_PATH}")
        sys.exit(1)
    MANIFEST_PATH.write_text(updated, encoding="utf-8")
    print(f"Manifest version updated to {version}")


def publish(platform: str) -> None:
    run([
        "msbuild", str(PROJECT_PATH),
        "/t:Publish",
        "/p:Configuration=Release",
        f"/p:Platform={platform}",
        "/p:AppxBundle=Never",
        "/p:UapAppxPackageBuildMode=SideloadOnly",
        "/p:AppxPackageSigningEnabled=false",
        "/p:GenerateAppxPackageOnBuild=true",
    ])


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

    branch   = f"release/{version}"
    tag      = f"v{version}"
    out_dir  = Path("Presentation") / "AppPackages" / f"Rok_{version}_{platform}_Test"

    if dry_run:
        print("[dry-run] No push will be performed")

    check_clean_working_tree()

    # 1. Create the release branch
    run(["git", "checkout", "-b", branch])

    # 2. Update Package.appxmanifest
    update_manifest(version)

    # 3. Commit the version bump
    run(["git", "add", str(MANIFEST_PATH)])
    run(["git", "commit", "-m", f"chore: bump version to {version}"])

    # 4. Build and publish the MSIX package
    publish(platform)

    # 5. Create an annotated tag
    run(["git", "tag", "-a", tag, "-m", f"Release {version}"])

    # 6. Push branch and tag (skipped in dry-run)
    if dry_run:
        print(f"\n[dry-run] Skipped: git push origin {branch}")
        print(f"[dry-run] Skipped: git push origin {tag}")
    else:
        run(["git", "push", "origin", branch])
        run(["git", "push", "origin", tag])

    print(f"\nRelease {tag} created successfully → origin/{branch}")
    print(f"Packages available in: {out_dir}")


if __name__ == "__main__":
    main()