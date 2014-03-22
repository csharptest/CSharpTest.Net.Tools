using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CSharpTest.Net.GeneratorsTest
{
    class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            TempPath = Path.GetTempFileName();
            File.Delete(TempPath);
            TempPath += Path.DirectorySeparatorChar;
            Directory.CreateDirectory(TempPath);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(TempPath, true);
            }
            catch { }
        }

        public string TempPath { get; private set; }
    }
}
