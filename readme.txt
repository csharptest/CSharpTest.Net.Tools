-----------------------------------------------------------------------------
CSharpTest.Net.Tools - http://csharptest.net
-----------------------------------------------------------------------------
ONLINE HELP
-----------------------------------------------------------------------------

Help on the command-line tools may be found at:

http://help.csharptest.net/?CommandLineTools.html


CmdTool help is located there at:
http://help.csharptest.net/?CmdToolOverview.html

CmdTool configuration information can be found at:

http://help.csharptest.net/?CmdToolConfiguration.html

-----------------------------------------------------------------------------
INSTALL GUIDE
-----------------------------------------------------------------------------

CmdTool integration with visual studio is discussed at:

http://help.csharptest.net/?CmdToolVisualStudio.html

In short:

1. From the tools/package directory run the following command from an 
   elevated command prompt:

   C:\Projects\...> CmdTool.exe register

   CmdTool supports Visual Studio 2008, 2010, 2012, and 2013


2. A sample configuration (CmdTool.config) is located in the content folder
   of the package (and should be copied to your project).  This file can be
   placed anywhere in the path above the file being generated.  Run the 
   following command to generate a sample configuration:

   C:\Projects\...> CmdTool.exe help-config


3. Right-click the desired source file. Select the "Properties" menu option.
   Under the entry called "Custom Tool" enter the value "CmdTool".


4. You can also re-run code generation tools for an entire project by using
   the following command line:

   C:\Projects\...> CmdTool.exe build *.csproj


-----------------------------------------------------------------------------

