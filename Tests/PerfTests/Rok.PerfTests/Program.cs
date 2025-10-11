// See https://aka.ms/new-console-template for more information
using Melodia.PerfTests;


BenchmarkDotNet.Reports.Summary summary = BenchmarkDotNet.Running.BenchmarkRunner.Run<GetFoldersByCreatDate>();