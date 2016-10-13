using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using System.Web.Mvc;
using IISDeployMVC.Exceptions;

namespace IISDeployMVC.Controllers
{

    public class DeployController : Controller
    {
        /*
         * TODO:
         * 1: Sørg for at gemme alle de filer som er blevet ændret i dette pull. TJEK
         * 2: Sørg for at angive hvilke filtyper, der skal ignoreres. (eksempelvis: .cs, .scss, _*.js). TJEK
         * 3: Sørg for at flytte alle opdaterede dll-filer over i produktion, hvis der har været ændringer i en eller flere .cs-filer. (ikke testet)
         * 4: Sørg for at flytte alle ændrede/tilføjede filer, som IKKE indgår i IgnoreFileTypes, over i produktion. (ikke testet)
         * 5: Sørg for at rydde _parsed-folderne, samt lignende foldere på dw-løsningerne i produktion. (ikke testet)
         * 6: 
         */

        private const string _ignoreFileTypes = "IgnoreFileTypes";

        private const string _buildRoot = "BuildRootDirectory";
        private const string _buildGulpJS = "BuildGulpJSDirectory";
        private const string _buildGulpCSS = "BuildGulpCSSDirectory";
        private const string _buildCompass = "BuildCompassDirectory";
        private const string _buildCsProj = "BuildCSProjFile";
        private const string _buildBranch = "BuildGitBranch";

        private const string _buildPathCSS = "BuildPathAssets";
        private const string _buildPathDLL = "BuildPathDLL";

        private const string _prodRoot = "ProdRootDirectory";
        private const string _prodPathCSS = "ProdPathCSS";
        private const string _prodPathJS = "ProdPathJS";
        private const string _prodPathDLL = "ProdPathDLL";

        private string _lastSha = "";

        Dictionary<string, bool> fileTypesToIgnore = ConfigurationManager.AppSettings[_ignoreFileTypes].Split(',').ToDictionary(x => x, x => true);

        private Dictionary<string, string> _changedFiles = new Dictionary<string, string>();

        private List<string> _buildedDlls;

        /*
         * TODO: Tjek om en fil både kan have statussen "C" og "R". På dette site kunne det godt se sådan ud nemlig: https://git-scm.com/docs/git-diff
         * Her er en god forklaring af hvordan copy og rename opfører sig: http://stackoverflow.com/questions/21697280/what-makes-c-and-r-filters-in-git-diff-diff-filter-cr-command
         */

        Dictionary<string, string> _copyStatusFlags = new Dictionary<string, string>()
            {
                { "M", "modified - File has been modified" },
                { "C", "copy-edit - File has been copied and modified" },
                { "A", "added - File has been added" }
            };

        Dictionary<string, string> _replaceStatusFlags = new Dictionary<string, string>()
            {
                { "R", "rename-edit - File has been renamed and modified" },
            };

        Dictionary<string, string> _deleteStatusFlags = new Dictionary<string, string>()
            {
                { "D", "deleted - File has been deleted" },
            };

        Dictionary<string, string> _exceptionStatusFlags = new Dictionary<string, string>()
            {
                { "U", "unmerged - File has conflicts after a merge" }
            };

        public String Index()
        {
            try
            {
                deploy();
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
            }

            return "deploy controller index";
        }

        private void deploy()
        {
            updateLastSha();

            pullFromGithub();

            buildListOfChangedFiles();

            checkForUnmergedFiles();

            updateLastSha();

            compileSassAndJavaScript();

            migrateFileChangesToProduction();

            if (anyBinaryFilesChanged())
            {
                compileCSharp();
                copyBinFilesToProduction();
            }

            cleanParsedFolders(ConfigurationManager.AppSettings[_prodRoot]);

        }

        //TODO: test this
        private void cleanParsedFolders(string sourceDirectory)
        {
            foreach (string directory in Directory.GetDirectories(sourceDirectory))
            {
                if (directory.Equals("_parsed"))
                {
                    foreach (string file in Directory.GetFiles(directory))
                    {
                        System.IO.File.Delete(file);
                    }
                }
                else
                {
                    cleanParsedFolders(directory);
                }
            }
        }

        //TODO: test this
        private void checkForUnmergedFiles()
        {
            List<string> unmergedFiles = _changedFiles.Values.ToList().Where(x => x.Equals("U")).ToList();

            if (unmergedFiles.Count > 0)
            {
                StringBuilder messageBuilder = new StringBuilder();
                messageBuilder.AppendLine("Pull from Github created merge-conflicts on the following files: ");
                foreach (string file in unmergedFiles)
                {
                    messageBuilder.AppendLine(file);
                }
                throw new UnmergedException(messageBuilder.ToString());
            }
        }

        //TODO: test this
        private void migrateFileChangesToProduction()
        {
            copyNewAndModifiedFilesToProduction();
            replaceRenamedFilesInProduction();
            deleteFilesInProduction();
        }

        //TODO: Test this
        private void copyNewAndModifiedFilesToProduction()
        {
            List<string> copyFilesList = new List<string>();

            foreach (KeyValuePair<string, string> file in _changedFiles)
            {
                if (!fileTypesToIgnore.ContainsKey(Path.GetExtension(file.Key)))
                {
                    if (_copyStatusFlags.ContainsKey(file.Value))
                    {
                        copyFilesList.Add(file.Key);
                    }
                }
            }

            foreach (string file in copyFilesList)
            {
                string copyDestination = file.Replace(ConfigurationManager.AppSettings[_buildRoot],
                    ConfigurationManager.AppSettings[_prodRoot]);
                System.IO.File.Copy(file, copyDestination);
            }
        }

        //TODO: test this
        private void replaceRenamedFilesInProduction()
        {
            List<string> renamedFilesList = new List<string>();

            foreach (KeyValuePair<string, string> file in _changedFiles)
            {
                if (!fileTypesToIgnore.ContainsKey(Path.GetExtension(file.Key)))
                {
                    if (_replaceStatusFlags.ContainsKey(file.Value))
                    {
                        renamedFilesList.Add(file.Key);
                    }
                }
            }

            foreach (string renamedFile in renamedFilesList)
            {
                string oldName = renamedFile.Split(new string[] { "\t" }, StringSplitOptions.None)[0];
                string newName = renamedFile.Split(new string[] { "\t" }, StringSplitOptions.None)[1];

                System.IO.File.Delete(oldName.Replace(ConfigurationManager.AppSettings[_buildRoot], ConfigurationManager.AppSettings[_prodRoot]));
                System.IO.File.Copy(newName, newName.Replace(ConfigurationManager.AppSettings[_buildRoot], ConfigurationManager.AppSettings[_prodRoot]));
            }
        }

        //TODO: Test this
        private void deleteFilesInProduction()
        {
            List<string> deletedFilesList = new List<string>();

            foreach (KeyValuePair<string, string> file in _changedFiles)
            {
                if (!fileTypesToIgnore.ContainsKey(Path.GetExtension(file.Key)))
                {
                    if (_deleteStatusFlags.ContainsKey(file.Value))
                    {
                        deletedFilesList.Add(file.Key);
                    }
                }
            }

            foreach (string deletedFile in deletedFilesList)
            {
                System.IO.File.Delete(deletedFile.Replace(ConfigurationManager.AppSettings[_buildRoot], ConfigurationManager.AppSettings[_prodRoot]));
            }
        }

        //TODO: test this
        private void copyBinFilesToProduction()
        {
            _buildedDlls.ForEach(x => System.IO.File.Copy(x, ConfigurationManager.AppSettings[_prodPathDLL]));
        }

        private bool anyBinaryFilesChanged()
        {
            return _changedFiles.Keys.Any(x => x.EndsWith(".cs"));
        }

        //TODO: Test this
        private void buildListOfChangedFiles()
        {
            string command = string.Format(@"cd {0} && git diff --name-status {1}", ConfigurationManager.AppSettings[_buildRoot], _lastSha);

            _changedFiles = executeCommandInTerminal(command)
                .Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .ToDictionary(
                x => x.Split(new string[] { "\t" }, StringSplitOptions.None)[1],
                x => x.Split(new string[] { "\t" }, StringSplitOptions.None)[0]);

            List<string> copyEditKeys = new List<string>();

            foreach (KeyValuePair<string, string> pair in _changedFiles)
            {
                if (pair.Value.Equals("C"))
                {
                    copyEditKeys.Add(pair.Key);
                }
            }

            foreach (string key in copyEditKeys)
            {
                _changedFiles[key] = _changedFiles[key].Split(new string[] { "\t" }, StringSplitOptions.None)[1];
            }

            Debug.WriteLine("testing list-result...");
        }

        private void updateLastSha()
        {
            string command = string.Format("cd {0} && git rev-parse HEAD", ConfigurationManager.AppSettings[_buildRoot]);

            _lastSha = executeCommandInTerminal(command).Replace("\n", "");

            Debug.WriteLine("LastSha updated to: " + _lastSha);
        }

        private void pullFromGithub()
        {
            string command = string.Format(@"cd {0} && git reset --hard HEAD && git pull origin {1}", ConfigurationManager.AppSettings[_buildRoot], ConfigurationManager.AppSettings[_buildBranch]);

            string result = executeCommandInTerminal(command);

            Debug.WriteLine("Result from terminal: " + result);
        }

        private void compileSassAndJavaScript()
        {
            compileCssWithGulp();
            //CompileJsWithGulp();
            //CompileCompassAndSusy();
        }

        private void compileCssWithGulp()
        {
            string command = string.Format(@"cd {0} && gulp sass-production",
                ConfigurationManager.AppSettings[_buildGulpCSS]);

            string result = executeCommandInTerminal(command);

            Debug.WriteLine("Result from terminal: " + result);
        }

        private void compileJsWithGulp()
        {
            string command = string.Format(@"cd {0} && gulp javascripts-production",
                ConfigurationManager.AppSettings[_buildGulpJS]);

            string result = executeCommandInTerminal(command);

            Debug.WriteLine("Result from terminal: " + result);
        }

        private void compileCompassAndSusy()
        {
            string command = string.Format(@"cd {0} && compass compile --output-style=compressed",
                ConfigurationManager.AppSettings[_buildCompass]);

            string result = executeCommandInTerminal(command);

            Debug.WriteLine("Result from terminal: " + result);
        }

        private bool compileCSharp()
        {
            Dictionary<string, string> props = new Dictionary<string, string>();
            props["Configuration"] = "Release";
            BuildRequestData request = new BuildRequestData(ConfigurationManager.AppSettings[_buildCsProj], props, null, new string[] { "Build" }, null);
            BuildParameters parms = new BuildParameters() { DetailedSummary = true };

            var result = BuildManager.DefaultBuildManager.Build(parms, request);

            _buildedDlls = result.ResultsByTarget["Build"].Items.Select(x => x.ItemSpec).ToList();

            return result.OverallResult == BuildResultCode.Success;
        }

        private string executeCommandInTerminal(string command)
        {
            ProcessStartInfo start = new ProcessStartInfo("cmd.exe ", "/c " + command);

            start.RedirectStandardOutput = true;

            start.UseShellExecute = false;

            start.CreateNoWindow = false;

            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo = start;
            proc.Start();
            proc.WaitForExit();

            string result = proc.StandardOutput.ReadToEnd();
            return result;
        }

        public string Welcome()
        {
            return "deploy controller welcome";
        }
    }
}
