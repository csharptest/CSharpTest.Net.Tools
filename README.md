CSharpTest.Net.Tools
=======================

CSharpTest.Net.Tools (moved from https://code.google.com/p/csharptest-net/)

## Change Log ##

2014-03-23	Initial clone and extraction from existing library.

## Online Help ##

Help on the command-line tools may be found at:
http://help.csharptest.net/?CommandLineTools.html

CmdTool help is located there at:
http://help.csharptest.net/?CmdToolOverview.html

CmdTool configuration information can be found at:
http://help.csharptest.net/?CmdToolConfiguration.html

## Installation ##

CmdTool integration with visual studio is discussed at:
http://help.csharptest.net/?CmdToolVisualStudio.html

### Quick start ###

From the tools/package directory run the following command from an elevated command prompt:
```
   C:\Projects\...> CmdTool.exe register
```
CmdTool supports Visual Studio 2008, 2010, 2012, and 2013

A sample configuration (CmdTool.config) is located in the content folder of the package (and should be copied to your project).  This file can be placed anywhere in the path above the file being generated.  Run the following command to generate a sample configuration:
```
   C:\Projects\...> CmdTool.exe help-config
```

Right-click the desired source file. Select the "Properties" menu option. Under the entry called "Custom Tool" enter the value "CmdTool".

You can also re-run code generation tools for an entire project by using
   the following command line:
```
   C:\Projects\...> CmdTool.exe build *.csproj
```

## Configuration Example ##

The following is a simple example of a configuration to run cmd.exe and prepend some text to the input file.  The input file must be a `.test` file, and the configuration file must be named `CmdTool.config` and located in the same directory or any direct ancestor.

```
<CmdTool>
  <match filespec="*.test">
    <generator debug="false" input-encoding="ascii" output-encoding="utf-8">
      <execute exe="%SystemRoot%\system32\Cmd.exe"/>
      <arg value="/c"/>
      <arg value="@Echo // Generated from input: $(inputPath) &amp;&amp; type $(InputPath)"/>
      <std-output extension=".Generated.cs"/>
    </generator>
  </match>
</CmdTool>
```
