using FluentAssertions;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Xunit;

namespace ServiceBusExplorer.Tests
{
    public class FileVersionShould
    {
        private string expectedVersion;

        public FileVersionShould()
        {
#if DEBUG
            expectedVersion = Environment.GetEnvironmentVariable("FILE_VERSION") ?? "1.0.0.1";
#else
            expectedVersion = Environment.GetEnvironmentVariable("FILE_VERSION") ??  throw new InvalidOperationException("file version not set by environment");
#endif
        }


        [Fact]
        public void FileShouldHaveSameVersionAsProvidedByBuild()
        {
            // Coral fork: the main assembly was renamed ServiceBusExplorer.exe ->
            // CoralEventExplorer.exe, so include it alongside the ServiceBus* libraries
            // to keep the expected count of 9.
            var assemblyNames = Directory.GetFiles(".", "ServiceBus*.*")
                    .Concat(Directory.GetFiles(".", "CoralEventExplorer*.*"))
                    .Where(x => x.EndsWith(".dll") || x.EndsWith(".exe"));

            var fileVersions = assemblyNames.Select(x => FileVersionInfo.GetVersionInfo(x).FileVersion);

            fileVersions.Should().HaveCount(9).And.AllBeEquivalentTo(expectedVersion);
        }
    }
}
