#region Copyright 2011 by Roger Knapp, Licensed under the Apache License, Version 2.0
/* Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *   http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using CSharpTest.Net.IO;
using NUnit.Framework;
using Commands = CSharpTest.Net.Generators.Commands;

namespace CSharpTest.Net.GeneratorsTest
{
    [TestFixture]
    public class TestResXtoMcByProject
    {
        #region Xml Project and Assembly Info
        const string ProjFormat = @"<Project>
  <PropertyGroup>
    <AssemblyName>SampleTestAssembly</AssemblyName>
    <RootNamespace>TestNamespace</RootNamespace>
    <OutputType>Library</OutputType>
    <ProjectGuid>{{45E678E9-5C41-4F8D-9040-9AA7AF0B000B}}</ProjectGuid>
    <ApplicationIcon>App.ico</ApplicationIcon>
    <ApplicationManifest>App.manifest</ApplicationManifest>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include='AssemblyInfo.cs' />
    <EmbeddedResource Include='{0}' />
  </ItemGroup>
</Project>";
        const string AsminfoFormat = @"
[assembly: System.Reflection.AssemblyTitle(""Generators Test"")]
[assembly: System.Reflection.AssemblyDescription(""Generators Test."")]
[assembly: System.Reflection.AssemblyProduct(""http://CSharpTest.Net/Projects"")]
[assembly: System.Reflection.AssemblyConfiguration(""Test"")]

[assembly: System.Reflection.AssemblyCompany(""Roger Knapp"")]
[assembly: System.Reflection.AssemblyCopyright(""Copyright 2010 by Roger Knapp, Licensed under the Apache License, Version 2.0"")]

[assembly: System.Reflection.AssemblyVersion(""1.2.3.4"")]
[assembly: System.Reflection.AssemblyFileVersion(""1.2.3.4"")]
";
        private const string AppManifest = @"
<?xml version=""1.0"" encoding=""utf-8""?>
<asmv1:assembly manifestVersion=""1.0"" xmlns=""urn:schemas-microsoft-com:asm.v1"" xmlns:asmv1=""urn:schemas-microsoft-com:asm.v1"" xmlns:asmv2=""urn:schemas-microsoft-com:asm.v2"" xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"">
  <assemblyIdentity version=""1.0.0.0"" name=""MyApplication.app""/>
  <trustInfo xmlns=""urn:schemas-microsoft-com:asm.v2"">
    <security>
      <requestedPrivileges xmlns=""urn:schemas-microsoft-com:asm.v3"">
        <requestedExecutionLevel level=""asInvoker"" uiAccess=""false"" />
      </requestedPrivileges>
    </security>
  </trustInfo>
</asmv1:assembly>
";
        #endregion
        [Test]
        public void TestGenerateWin32Resource()
        {
            /* 
             * Required: This generation type is used to embed the message resource into the current assembly
             * by using a custom Win32Resource setting in the project, for this example you would use:
             *     <Win32Resource>Resources\TestResXClass1.res</Win32Resource>
             *     
             * FYI: You must manually add this to the project or else VStudio will erase <ApplicationIcon>
             * The generated res file contains the <ApplicationIcon> as well as assembly version information
             * from the "AssemblyInfo.cs" file included in the project.  If you want to manually supply the
             * assembly info you can point it to a specific assemblyInfo.cs file, or to a dll to extract it
             * from.
             * 
             * Optionally you may include "Resources\TestResXClass1.Constants.cs" to define constants for all
             * hresults, facilities, and categories that are defined.
             * 
             * Optionally you may include "Resources\TestResXClass1.InstallUtil.cs" to define an installer for
             * the event log registration.  If you need modifications to this installer, simply copy and paste
             * and use it for a starting point.
             */
            using (TempDirectory tmp = new TempDirectory())
            using (TempFile asminfo = TempFile.Attach(Path.Combine(tmp.TempPath, "AssemblyInfo.cs")))
            using (TempFile resx1 = TempFile.Attach(Path.Combine(tmp.TempPath, "TestResXClass1.resx")))
            using (TempFile csproj = TempFile.Attach(Path.Combine(tmp.TempPath, "TestResXClass1.csproj")))
            using (TempFile manifest = TempFile.Attach(Path.Combine(tmp.TempPath, "App.manifest")))
            using (TempFile appico = TempFile.Attach(Path.Combine(tmp.TempPath, "App.ico")))
            {
                asminfo.WriteAllText(AsminfoFormat);
                csproj.WriteAllText(String.Format(ProjFormat, Path.GetFileName(resx1.TempPath)));
                manifest.WriteAllText(AppManifest);

                TestResourceBuilder builder1 = new TestResourceBuilder("TestNamespace", "TestResXClass1");
                builder1.Add(".AutoLog", true);
                builder1.Add(".EventSource", "HelloWorld");
                builder1.Add("Value1", "value for 1", "#MessageId = 1");
                builder1.BuildResX(resx1.TempPath);

                using (Stream s = appico.Open())
                    Properties.Resources.App.Save(s);

                Generators.Commands.ProjectResX(csproj.TempPath, @"Resources\TestResXClass1", null, String.Empty, Path.GetDirectoryName(TestResourceBuilder.FindExe("mc.exe")));

                Assert.IsTrue(Directory.Exists(Path.Combine(tmp.TempPath, "Resources")));
                Assert.IsTrue(File.Exists(Path.Combine(tmp.TempPath, @"Resources\MSG00409.bin")));
                Assert.IsTrue(File.Exists(Path.Combine(tmp.TempPath, @"Resources\TestResXClass1.mc")));
                Assert.IsTrue(File.Exists(Path.Combine(tmp.TempPath, @"Resources\TestResXClass1.h")));
                Assert.IsTrue(File.Exists(Path.Combine(tmp.TempPath, @"Resources\TestResXClass1.rc")));
                Assert.IsTrue(File.ReadAllText(Path.Combine(tmp.TempPath, @"Resources\TestResXClass1.rc")).Contains("1.2.3.4"));
                Assert.IsTrue(File.Exists(Path.Combine(tmp.TempPath, @"Resources\TestResXClass1.res")));
                Assert.IsTrue(File.Exists(Path.Combine(tmp.TempPath, @"Resources\TestResXClass1.Constants.cs")));
                Assert.IsTrue(File.Exists(Path.Combine(tmp.TempPath, @"Resources\TestResXClass1.InstallUtil.cs")));
            }
        }
        [Test]
        public void TestProjectResXVersionByAssembly()
        {
            /* Demonstrates versioning from an existing assembly... */
            using (TempDirectory tmp = new TempDirectory())
            using (TempFile asminfo = TempFile.Attach(Path.Combine(tmp.TempPath, "AssemblyInfo.cs")))
            using (TempFile resx1 = TempFile.Attach(Path.Combine(tmp.TempPath, "TestResXClass1.resx")))
            using (TempFile csproj = TempFile.Attach(Path.Combine(tmp.TempPath, "TestResXClass1.csproj")))
            using (TempFile manifest = TempFile.Attach(Path.Combine(tmp.TempPath, "App.manifest")))
            using (TempFile appico = TempFile.Attach(Path.Combine(tmp.TempPath, "App.ico")))
            {
                asminfo.WriteAllText(AsminfoFormat);
                csproj.WriteAllText(String.Format(ProjFormat, Path.GetFileName(resx1.TempPath)));
                manifest.WriteAllText(AppManifest);

                TestResourceBuilder builder1 = new TestResourceBuilder("TestNamespace", "TestResXClass1");
                builder1.Add(".AutoLog", true);
                builder1.Add(".EventSource", "HelloWorld");
                builder1.Add("Value1", "value for 1", "#MessageId = 1");
                builder1.BuildResX(resx1.TempPath);

                using (Stream s = appico.Open())
                    Properties.Resources.App.Save(s);

                Generators.Commands.ProjectResX(csproj.TempPath, @"Resources\TestResXClass1",
                    typeof(Generators.Commands).Assembly.Location, String.Empty, 
                    Path.GetDirectoryName(TestResourceBuilder.FindExe("mc.exe")));

                Assert.IsTrue(Directory.Exists(Path.Combine(tmp.TempPath, "Resources")));
                Assert.IsTrue(File.Exists(Path.Combine(tmp.TempPath, @"Resources\MSG00409.bin")));
                Assert.IsTrue(File.Exists(Path.Combine(tmp.TempPath, @"Resources\TestResXClass1.mc")));
                Assert.IsTrue(File.Exists(Path.Combine(tmp.TempPath, @"Resources\TestResXClass1.h")));
                Assert.IsTrue(File.Exists(Path.Combine(tmp.TempPath, @"Resources\TestResXClass1.rc")));
                Assert.IsTrue(File.ReadAllText(Path.Combine(tmp.TempPath, @"Resources\TestResXClass1.rc"))
                    .Contains(typeof(Generators.Commands).Assembly.GetName().Version.ToString()));
                Assert.IsTrue(File.Exists(Path.Combine(tmp.TempPath, @"Resources\TestResXClass1.res")));
                Assert.IsTrue(File.Exists(Path.Combine(tmp.TempPath, @"Resources\TestResXClass1.Constants.cs")));
                Assert.IsTrue(File.Exists(Path.Combine(tmp.TempPath, @"Resources\TestResXClass1.InstallUtil.cs")));
            }
        }
        [Test]
        public void TestCreateMessageAssembly()
        {
            /* 
             * This is the most simple form of message generation in which we generate a complete dll with
             * the message resource (and optional versioning).  The dll includes the TestResXClass1.Constants.cs
             * and the TestResXClass1.InstallUtil.cs as generated in the example TestGenerateWin32Resource().
             */
            using (TempDirectory tmp = new TempDirectory())
            using (TempFile asminfo = TempFile.Attach(Path.Combine(tmp.TempPath, "AssemblyInfo.cs")))
            using (TempFile resx1 = TempFile.Attach(Path.Combine(tmp.TempPath, "TestResXClass1.resx")))
            using (TempFile dllout = TempFile.Attach(Path.Combine(tmp.TempPath, "TestResXClass1.dll")))
            {
                asminfo.WriteAllText(AsminfoFormat);

                TestResourceBuilder builder1 = new TestResourceBuilder("TestNamespace", "TestResXClass1");
                builder1.Add(".AutoLog", true);
                builder1.Add(".EventSource", "HelloWorld");
                builder1.Add("Value1", "value for 1", "#MessageId = 1");
                builder1.BuildResX(resx1.TempPath);

                Generators.Commands.ResXtoMessageDll(dllout.TempPath, new string[] { resx1.TempPath },
                    tmp.TempPath, asminfo.TempPath, "TestNamespace", Path.GetDirectoryName(TestResourceBuilder.FindExe("mc.exe")), "/debug-");

                Assert.IsTrue(dllout.Exists);
            }
        }
        [Test, ExpectedException(typeof(ApplicationException))]
        public void TestProjectResXWithBadAssemblyInfo()
        {
            /* Bad assembly info... */
            using (TempDirectory tmp = new TempDirectory())
            using (TempFile asminfo = TempFile.Attach(Path.Combine(tmp.TempPath, "AssemblyInfo.cs")))
            using (TempFile resx1 = TempFile.Attach(Path.Combine(tmp.TempPath, "TestResXClass1.resx")))
            using (TempFile csproj = TempFile.Attach(Path.Combine(tmp.TempPath, "TestResXClass1.csproj")))
            using (TempFile manifest = TempFile.Attach(Path.Combine(tmp.TempPath, "App.manifest")))
            using (TempFile appico = TempFile.Attach(Path.Combine(tmp.TempPath, "App.ico")))
            {
                asminfo.WriteAllText(AsminfoFormat + "[InvalidAttribute]");
                csproj.WriteAllText(String.Format(ProjFormat, Path.GetFileName(resx1.TempPath)));
                manifest.WriteAllText(AppManifest);

                TestResourceBuilder builder1 = new TestResourceBuilder("TestNamespace", "TestResXClass1");
                builder1.Add(".AutoLog", true);
                builder1.Add(".EventSource", "HelloWorld");
                builder1.Add("Value1", "value for 1", "#MessageId = 1");
                builder1.BuildResX(resx1.TempPath);

                using (Stream s = appico.Open())
                    Properties.Resources.App.Save(s);

                Generators.Commands.ProjectResX(csproj.TempPath, @"Resources\TestResXClass1", null, String.Empty, Path.GetDirectoryName(TestResourceBuilder.FindExe("mc.exe")));
            }
        }
    }
}
