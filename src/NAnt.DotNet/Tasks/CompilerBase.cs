// NAnt - A .NET build tool
// Copyright (C) 2001-2002 Gerry Shaw
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA

// Gerry Shaw (gerry_shaw@yahoo.com)
// Mike Krueger (mike@icsharpcode.net)
// Ian MacLean ( ian_maclean@another.com )

using System;
using System.Collections.Specialized;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

using SourceForge.NAnt.Attributes;

namespace SourceForge.NAnt.Tasks {

    /// <summary>Provides the abstract base class for a Microsoft compiler task.</summary>
    public abstract class CompilerBase : ExternalProgramBase {

        string _responseFileName;

        // Microsoft common compiler options        
        string _output = null;        
        string _target = null;              
        bool _debug = false;      
        string _define = null;       
        string _win32icon = null;      
        FileSet _references = new FileSet();        
        ResourceFileSet _resources = new ResourceFileSet();       
        FileSet _modules = new FileSet();       
        FileSet _sources = new FileSet();        
        ResGenTask _resgenTask = null;

        /// <summary>Output directory for the compilation target.</summary>
        [TaskAttribute("output", Required=true)]
        public string Output        { get { return _output; } set { _output = value; }}

        /// <summary>Output type (<c>library</c> or <c>exe</c>).</summary>
        [TaskAttribute("target", Required=true)]
        public string OutputTarget  { get { return _target; } set { _target = value; }}

        /// <summary>Generate debug output (<c>true</c>/<c>false</c>).</summary>
        [BooleanValidator()]
        [TaskAttribute("debug")]
        public bool Debug           { get { return Convert.ToBoolean(_debug); } set { _debug = value; }}

        /// <summary>Define conditional compilation symbol(s). Corresponds to <c>/d[efine]:</c> flag.</summary>
        [TaskAttribute("define")]
        public string Define        { get { return _define; } set { _define = value; }}

        /// <summary>Icon to associate with the application. Corresponds to <c>/win32icon:</c> flag.</summary>
        [TaskAttribute("win32icon")]
        public string Win32Icon     { get { return _win32icon; } set { _win32icon = value; }}

        /// <summary>Reference metadata from the specified assembly files.</summary>
        [FileSet("references")]
        public FileSet References   { get { return _references; } set { _references = value; }}

        /// <summary>Set resources to embed.</summary>
        ///<remarks>This can be a combination of resx files and file resources. .resx files will be compiled by resgen and then embedded into the 
        ///resulting executable. The Prefix attribute is used to make up the resourcename added to the assembly manifest for non resx files. For resx files the namespace from the matching source file is used as the prefix. 
        ///This matches the behaviour of Visual Studio </remarks>
        [FileSet("resources")]
        public ResourceFileSet Resources    { get { return _resources; } set { _resources = value; }}

        /// <summary>Link the specified modules into this assembly.</summary>
        [FileSet("modules")]
        public FileSet Modules      { get { return _modules; } set { _modules = value; }}

        /// <summary>The set of source files for compilation.</summary>
        [FileSet("sources")]
        public FileSet Sources { get { return _sources; } set { _sources = value; }}

        public override string ProgramFileName  {
            get { 
                // Instead of relying on the .NET compilers to be in the user's path point
                // to the compiler directly since it lives in the .NET Framework's runtime directory
                return Path.Combine(Path.GetDirectoryName(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory()), Name);
            } 
        }

        public override string ProgramArguments { get { return "@" + _responseFileName; } }

        
        /// <summary>Allows derived classes to provide compiler-specific options.</summary>
        protected virtual void WriteOptions(TextWriter writer) {
        }

        /// <summary>Write an option using the default output format.</summary>
        protected virtual void WriteOption(TextWriter writer, string name) {
            writer.WriteLine("/{0}", name);
        }

        /// <summary>Write an option and its value using the default output format.</summary>
        protected virtual void WriteOption(TextWriter writer, string name, string arg) {           
            // Always quote arguments ( )
            writer.WriteLine("\"/{0}:{1}\"", name, arg);          
        }
    
        /// <summary>Gets the complete output path.</summary>
        protected string GetOutputPath() {
            return Path.GetFullPath(Path.Combine(BaseDirectory, Output));
        }

        /// <summary>Determines whether compilation is needed.</summary>
        protected virtual bool NeedsCompiling() {
            // return true as soon as we know we need to compile

            FileInfo outputFileInfo = new FileInfo(GetOutputPath());
            if (!outputFileInfo.Exists) {
                return true;
            }

            //Sources Updated?
            string fileName = FileSet.FindMoreRecentLastWriteTime(Sources.FileNames, outputFileInfo.LastWriteTime);
            if (fileName != null) {
                Log.WriteLineIf(Verbose, LogPrefix + "{0} is out of date, recompiling.", fileName);
                return true;
            }

            //References Updated?
            fileName = FileSet.FindMoreRecentLastWriteTime(References.FileNames, outputFileInfo.LastWriteTime);
            if (fileName != null) {
                Log.WriteLineIf(Verbose, LogPrefix + "{0} is out of date, recompiling.", fileName);
                return true;
            }

            //Modules Updated?
            fileName = FileSet.FindMoreRecentLastWriteTime(Modules.FileNames, outputFileInfo.LastWriteTime);
            if (fileName != null) {
                Log.WriteLineIf(Verbose, LogPrefix + "{0} is out of date, recompiling.", fileName);
                return true;
            }

            //Resources Updated?
            fileName = FileSet.FindMoreRecentLastWriteTime(Resources.FileNames, outputFileInfo.LastWriteTime);
            if (fileName != null) {
                Log.WriteLineIf(Verbose, LogPrefix + "{0} is out of date, recompiling.", fileName);
                return true;
            }
 
            // check the args for /res or /resource options.
            StringCollection resourceFileNames = new StringCollection();
            foreach (string arg in Args) {
                if (arg.StartsWith("/res:") || arg.StartsWith("/resource:")) {
                    string path = arg.Substring(arg.IndexOf(':') + 1);

                    int indexOfComma = path.IndexOf(',');
                    if (indexOfComma != -1) {
                        path = path.Substring(0, indexOfComma);
                    }
                    resourceFileNames.Add(path);
                }
            }
            fileName = FileSet.FindMoreRecentLastWriteTime(resourceFileNames, outputFileInfo.LastWriteTime);
            if (fileName != null) {
                Log.WriteLineIf(Verbose, LogPrefix + "{0} is out of date, recompiling.", fileName);
                return true;
            }

            // if we made it here then we don't have to recompile
            return false;
        }

        protected override void ExecuteTask() {
            if (NeedsCompiling()) {
                // create temp response file to hold compiler options
                _responseFileName = Path.GetTempFileName();
                StreamWriter writer = new StreamWriter(_responseFileName);

                try {
                    if (References.BaseDirectory == null) {
                        References.BaseDirectory = BaseDirectory;
                    }
                    if (Modules.BaseDirectory == null) {
                        Modules.BaseDirectory = BaseDirectory;
                    }
                    if (Sources.BaseDirectory == null) {
                        Sources.BaseDirectory = BaseDirectory;
                    }

                    Log.WriteLine(LogPrefix + "Compiling {0} files to {1}", Sources.FileNames.Count, GetOutputPath());

                    // specific compiler options
                    WriteOptions(writer);

                    // Microsoft common compiler options
                    WriteOption(writer, "nologo");
                    WriteOption(writer, "target", OutputTarget);
                    if (Define != null) {                       
                        WriteOption(writer, "define", Define);
                    }
                                        
                    WriteOption(writer, "out", GetOutputPath());
                    if (Win32Icon != null) {
                        WriteOption(writer, "win32icon", Win32Icon);                        
                    }
                    //TODO: I changed the References.FileNames to References.Includes
                    //      Otherwise the system dlls never make it into the references.
                    //      Not too sure of the other implications of this change, but the
                    //      new Nant can run it's own build in addition to the VB6 stuff 
                    //      I've thrown at it.
                    // gs: This will not work when the user gives a reference in terms of a pattern.
                    // The problem is with the limitaions of the FileSet scanner.
                    // I've changed it back to .FileNames for now
                    foreach (string fileName in References.FileNames) {                       
                        WriteOption(writer, "reference", fileName);
                    }
                    foreach (string fileName in Modules.FileNames) {
                        WriteOption(writer, "addmodule", fileName);                        
                    }
                    
                    // compile resources
                    if( Resources.ResxFiles.FileNames.Count > 0 ) {
                        CompileResxResources( Resources.ResxFiles );
                    }
                    
                    // Resx args
                    foreach (string fileName in Resources.ResxFiles.FileNames ) {
                        string prefix = GetFormNamespace(fileName); // try and get it from matching form
                        if (prefix == "")
                            prefix = Resources.Prefix;                        
                        string actualFileName = Path.GetFileNameWithoutExtension(fileName);
                        string tmpResourcePath = Path.ChangeExtension( fileName, "resources" );                                                       
                        string manifestResourceName = Path.GetFileName(tmpResourcePath);
                        if(prefix != "")
                            manifestResourceName = manifestResourceName.Replace(actualFileName, prefix + "." + actualFileName );          
                        string resourceoption = tmpResourcePath + "," + manifestResourceName;    
                        WriteOption(writer, "resource", resourceoption);          
                    }
                    
                    // other resources
                    foreach (string fileName in Resources.NonResxFiles.FileNames ) {                                                                                                        
                        string actualFileName = Path.GetFileNameWithoutExtension(fileName);                                                                                  
                        string manifestResourceName = Path.GetFileName(fileName);
                        if(Resources.Prefix != "")
                            manifestResourceName = manifestResourceName.Replace(actualFileName, Resources.Prefix + "." + actualFileName );                       
                        string resourceoption = fileName + "," + manifestResourceName;                                                                                                                   
                        WriteOption(writer, "resource", resourceoption);     
                    }

                    foreach (string fileName in Sources.FileNames) {                      
                       writer.WriteLine("\"" + fileName + "\"");
                    }

                    // Make sure to close the response file otherwise contents
                    // will not be written to disc and EXecuteTask() will fail.
                    writer.Close();

                    if (Verbose) {
                        // display response file contents
                        Log.WriteLine(LogPrefix + "Contents of " + _responseFileName);
                        StreamReader reader = File.OpenText(_responseFileName);
                        Log.WriteLine(reader.ReadToEnd());
                        reader.Close();
                    }

                    // call base class to do the work
                    base.ExecuteTask();

                    // clean up generated resources.
                    if (_resgenTask != null )
                        _resgenTask.RemoveOutputs();

                } finally {
                    // make sure we delete response file even if an exception is thrown
                    writer.Close(); // make sure stream is closed or file cannot be deleted
                    File.Delete(_responseFileName);                    
                    _responseFileName = null;
                }
            }
        }
        /// <summary>
        /// To be overidden by derived classes. Used to determine the file Extension required by the current compiler
        /// </summary>
        /// <returns></returns>
        protected abstract string GetExtension();
                
        /// <summary>
        /// Open matching source file to find the correct namespace. This may need to be overidden by 
        /// the particular compiler if the namespace syntax is different for that language 
        /// </summary>
        /// <param name="resxPath"></param>
        /// <returns></returns>
        protected virtual string GetFormNamespace(string resxPath){
            
            // Determine Extension for compiler type
            string extension = GetExtension();
            string retnamespace = "";
            
            // open matching source file if it exists
            string sourceFile = resxPath.Replace( "resx", extension );
        
            StreamReader sr=null;
            try {
                sr = File.OpenText( sourceFile );
             
                while ( sr.Peek() > -1 ) {                               
                string str = sr.ReadLine();
                string matchnamespace =  @"namespace ((\w+.)*)";   		    		
                string matchnamespaceCaps =  @"Namespace ((\w+.)*)";   	
                Regex matchNamespaceRE = new Regex(matchnamespace);
                Regex matchNamespaceCapsRE = new Regex(matchnamespaceCaps);
                    
                if (matchNamespaceRE.Match(str).Success ){
                    Match namematch = matchNamespaceRE.Match(str);
                    retnamespace = namematch.Groups[1].Value; 
                    retnamespace = retnamespace.Replace( "{", "" );
                    retnamespace = retnamespace.Trim();
                    break;
                }
                else  if (matchNamespaceCapsRE.Match(str).Success ) {
                    Match namematch = matchNamespaceCapsRE.Match(str);
                    retnamespace = namematch.Groups[1].Value;                     
                    retnamespace = retnamespace.Trim();
                    break;
                }
              }
            } catch(System.IO.FileNotFoundException) { // if no matching file, dump out
                return "";
            } finally {
                if(sr != null)
                    sr.Close();
            }
            return retnamespace;
        }

        /// <summary>
        /// Compile the resx files to temp .resources files
        /// </summary>
        /// <param name="resourceFileSet"></param>
        protected void CompileResxResources(FileSet resourceFileSet ) {
            ResGenTask resgen = new ResGenTask();                                     
            resgen.Resources = resourceFileSet; // set the fileset
            resgen.Verbose = false; 
            resgen.Parent = this.Parent;
            resgen.Project = this.Project;     
            resgen.ToDirectory = this.BaseDirectory;
                      
            _resgenTask = resgen;
            
            // Fix up the indent level --
            Log.IndentLevel++;
            resgen.Execute();
            Log.IndentLevel--;
        }
    }
}
