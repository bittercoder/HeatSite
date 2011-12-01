using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace HeatSite
{
    public class SiteDistiller
    {
        static readonly XNamespace WixNameSpace = "http://schemas.microsoft.com/wix/2006/wi";
        
        [Argument(ArgumentType.AtMostOnce, HelpText = "The component group name given to the fragments", ShortName = "comp", DefaultValue = "site.content")] public string ComponentGroupName;
        [Argument(ArgumentType.AtMostOnce, HelpText = "The destination file where the wix fragments will be written", ShortName = "out", DefaultValue = "HeatSiteOutput.wxs")] public string OutputPath;
        [Argument(ArgumentType.Required, HelpText = "The root directory of the site project", ShortName = "root")] public string Root;
        [Argument(ArgumentType.AtMostOnce, HelpText = "The directory declared in the wix file that these files will be within", ShortName = "siteDir", DefaultValue = "site")] public string SiteDirectory;
        [Argument(ArgumentType.Required, HelpText = "The root path appended to the paths in the fragments, normally this would be a relative path from the installer project to the source files i.e. ..\\..\\WebApplication\\", ShortName = "src")] public string Source;
        [Argument(ArgumentType.AtMostOnce, HelpText = "If true then .config files will be marked as don't overwrite", ShortName = "dontOverwriteConfigs")] public bool DontOverwriteFilesWithConfigExtension;

        public void Execute()
        {
            ToDocument().Save(OutputPath);
        }

        public XDocument ToDocument()
        {
            IEnumerable<string> allFiles = Root.AllFiles().ExcludeBlackListFiles().ToRelative(Root);
            IEnumerable<string> allDirectories =
                from directory in allFiles.AllDirectories().ToRelative(Root)
                where directory.Length > 1
                select directory;
            IEnumerable<XElement> fragments = (
                                                  from file in allFiles
                                                  select CreateFileFragment(file)).Concat(
                                                      from directory in allDirectories
                                                      select CreateDirectoryFragment(directory)).Concat(new[]
                                                          {
                                                              ToComponentGroupFragment(allFiles)
                                                          });
            return new XDocument(new object[]
                {
                    FileListComment(allFiles),
                    new XElement(WixNameSpace + "Wix", fragments)
                });
        }

        public XComment FileListComment(IEnumerable<string> files)
        {
            var builder = new StringBuilder();
            builder.AppendLine("File list built with DevDefined HeatSite at " + DateTime.Now);
            foreach (string file in files)
            {
                builder.AppendLine(file);
            }
            return new XComment(builder.ToString());
        }

        public XElement ToComponentGroupFragment(IEnumerable<string> files)
        {
            var componentGroup = new XElement(WixNameSpace + "ComponentGroup", new XAttribute("Id", ComponentGroupName));
            componentGroup.Add(
                from file in files
                select new XElement(WixNameSpace + "ComponentRef", new XAttribute("Id", file.ToWixId())));
            return new XElement(WixNameSpace + "Fragment", componentGroup);
        }

        public XElement CreateFileFragment(string file)
        {
            string directory = file.DirectoryName();
            string directoryRef = string.IsNullOrEmpty(directory) ? SiteDirectory : directory.ToWixId();
            string fileId = file.ToWixId();
            string fileName = file.NameOnly();

            var component = new XElement(WixNameSpace + "Component", new object[]
                {
                    new XAttribute("Id", fileId),
                    new XAttribute("Guid", "*"),
                    new XElement(WixNameSpace + "File", new object[]
                        {
                            new XAttribute("Id", fileId),
                            new XAttribute("Name", fileName),
                            new XAttribute("KeyPath", "yes"),
                            new XAttribute("Source", Source + file)
                        })
                });

            if (DontOverwriteFilesWithConfigExtension && fileName.EndsWith(".config",StringComparison.OrdinalIgnoreCase))
            {
                component.Add(new XAttribute("NeverOverwrite", "yes"));
            }

            return new XElement(WixNameSpace + "Fragment", new XElement(WixNameSpace + "DirectoryRef", new object[]
                {
                    new XAttribute("Id", directoryRef),
                    component 
                }));
        }

        public XElement CreateDirectoryFragment(string directory)
        {
            string parentDirectory = directory.ParentDirectory();
            string parentDirectoryRef = string.IsNullOrEmpty(parentDirectory) ? SiteDirectory : parentDirectory.ToWixId();
            string directoryId = directory.ToWixId();
            string directoryName = directory.NameOnly();
            return new XElement(WixNameSpace + "Fragment", new XElement(WixNameSpace + "DirectoryRef", new object[]
                {
                    new XAttribute("Id", parentDirectoryRef),
                    new XElement(WixNameSpace + "Directory", new object[]
                        {
                            new XAttribute("Id", directoryId),
                            new XAttribute("Name", directoryName)
                        })
                }));
        }
    }
}