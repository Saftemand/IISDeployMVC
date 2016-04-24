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
using System.Web.Http;
using System.Web.Mvc;

namespace IISDeployMVC.Controllers
{
    public class DeployController : Controller
    {
        public String Index() {
            try
            {
                /* 
                 * MSBuild:
                 * 
                 * Compile the application, create the folder, put files in some folders, 
                 * 
                 * What is a build:
                 * A build is the steps that you want to run:
                 * - Compiling the application
                 * - Creating folders
                 * - Moving the compiled files
                 * - Deploying files
                 * 
                 * What you need to know to start:
                 * - What parameters that you will use throughout the build (properties)
                 * - What the build will do throughout the build process (tasks)
                 * - How to organize my build process steps in desirable workflow manner and how to start from a specific point (targets, conditions)
                 * - What you will build (items)
                 */


                PullFromGithub();

                CompileSassAndJavaScript();

                CompileCSharp();
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp.Message);
            }
            Console.WriteLine("hit me!");
            return "deploy controller index";
        }

        private void Deploy() { 
            
        }

        private void PullFromGithub() {
            string command = string.Format(@"cd {0} && git reset --hard HEAD && git pull", @"C:\Users\Michael\Downloads\wrap-communityedition\S_DW_Continuous_Deployment_Test");

            ProcessStartInfo start = new ProcessStartInfo("cmd.exe ", "/c " + command);

            start.RedirectStandardOutput = true;

            start.UseShellExecute = false;

            start.CreateNoWindow = false;

            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo = start;
            proc.Start();
            proc.WaitForExit();

            string result = proc.StandardOutput.ReadToEnd();

            Debug.WriteLine("Result from terminal: " + result);
        }

        private void CompileSassAndJavaScript() {
            CompileCSSWithGulp();
            CompileJSWithGulp();
            //CompileCompassAndSusy();
        }

        private void CompileCSSWithGulp() {
            string command = string.Format(@"cd {0} && gulp sass-production",
                @"C:\Users\Michael\Downloads\wrap-communityedition\S_DW_Continuous_Deployment_Test\S_DW_Continuous_Deployment_Test\Files\Templates\Designs\Continuous Deployment\sources");

            ProcessStartInfo start = new ProcessStartInfo("cmd.exe ", "/c " + command);

            start.RedirectStandardOutput = true;

            start.UseShellExecute = false;

            start.CreateNoWindow = false;

            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo = start;
            proc.Start();
            proc.WaitForExit();

            string result = proc.StandardOutput.ReadToEnd();

            Debug.WriteLine("Result from terminal: " + result);
        }

        private void CompileJSWithGulp(){
            string command = string.Format(@"cd {0} && gulp javascripts-production",
                @"C:\Users\Michael\Downloads\wrap-communityedition\S_DW_Continuous_Deployment_Test\S_DW_Continuous_Deployment_Test\Files\Templates\Designs\Continuous Deployment\sources");

            ProcessStartInfo start = new ProcessStartInfo("cmd.exe ", "/c " + command);

            start.RedirectStandardOutput = true;

            start.UseShellExecute = false;

            start.CreateNoWindow = false;

            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo = start;
            proc.Start();
            proc.WaitForExit();

            string result = proc.StandardOutput.ReadToEnd();

            Debug.WriteLine("Result from terminal: " + result);
        }

        private void CompileCompassAndSusy() {
            string command = string.Format(@"cd {0} && compass compile --output-style=compressed",
                @"C:\Users\Michael\Downloads\wrap-communityedition\S_DW_Continuous_Deployment_Test\S_DW_Continuous_Deployment_Test\Files\Templates\Designs\Continuous Deployment");

            ProcessStartInfo start = new ProcessStartInfo("cmd.exe ", "/c " + command);

            start.RedirectStandardOutput = true;

            start.UseShellExecute = false;

            start.CreateNoWindow = false;

            System.Diagnostics.Process proc = new System.Diagnostics.Process();
            proc.StartInfo = start;
            proc.Start();
            proc.WaitForExit();

            string result = proc.StandardOutput.ReadToEnd();

            Debug.WriteLine("Result from terminal: " + result);
        }

        private bool CompileCSharp() {
            var props = new Dictionary<string, string>();
            props["Configuration"] = "Release";
            var request = new BuildRequestData(@"C:\Users\Michael\Downloads\wrap-communityedition\S_DW_Continuous_Deployment_Test\S_DW_Continuous_Deployment_test\S_DW_Continuous_Deployment_test.csproj", props, null, new string[] { "Build" }, null);
            var parms = new BuildParameters();

            var result = BuildManager.DefaultBuildManager.Build(parms, request);
            
            return result.OverallResult == BuildResultCode.Success;
        }

        public String Welcome()
        {
            return "deploy controller welcome";
        }
    }
}
