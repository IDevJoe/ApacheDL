using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Mono.Options;

namespace ApacheDL
{
    internal class Program
    {
        private static bool Debug = false;
        private static string Url;
        private static string OutputDirectory = "out";
        private static bool DoneQueueing = false;
        private static List<string> Queue = new List<string>();
        public static void Main(string[] args)
        {
            bool help = false;
            var options = new OptionSet()
            {
                {"d|debug", "Show debug messages", (x) => { Debug = true; }},
                {"u|url=", "Specify the URL to download from", (x) => { Url = x;}},
                {"o|output=", "Specify the output directory, defaults to 'out'", (x) => { OutputDirectory = x;}},
                {"h|help", "Show this", (x) => { help = true;}}
            };
            var res = options.Parse(args);
            if (help)
            {
                Console.WriteLine("ApacheDL -u [url]");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }
            if (Url == null)
            {
                Console.WriteLine("You must specify a url using --url");
                return;
            }

            var tsk = DownloadThread();
            getURLs();
            tsk.GetAwaiter().GetResult();
        }

        public static void getURLs()
        {
            var abs = Path.GetFullPath(OutputDirectory);
            using (WebClient client = new WebClient())
            {
                string resp = client.DownloadString(Url);
                HtmlDocument document = new HtmlDocument();
                document.LoadHtml(resp);
                var node = document.DocumentNode.SelectSingleNode("//table");
                var rows = node.SelectNodes("tr");
                Console.WriteLine(rows.Count-3 + " rows.");
                int crow = 0;
                Uri uri1 = new Uri(Url);
                foreach (var htmlNode in rows)
                {
                    crow++;
                    if (crow < 4) continue;
                    if (htmlNode.SelectNodes("td") == null) continue;
                    var y = htmlNode.SelectSingleNode("td[1]/img");
                    var type = y.GetAttributeValue("alt", "Unknown");
                    var linkattr = htmlNode.SelectSingleNode("td[2]/a");
                    var fname = linkattr.GetAttributeValue("href", "#");
                    
                    if (Directory.Exists(abs))
                    {
                        var destpth = Path.Combine(abs, fname);
                        if (File.Exists(destpth)) continue;
                    }

                    if (type == "[DIR]") continue;
                    if (type != "[IMG]" && type != "[VID]")
                    {
                        Console.WriteLine($"File '{fname}' is listed as {type}. Do you want to download it anyway? (y/n)");
                        var rl = Console.ReadLine();
                        if (rl == "n") continue;
                    }
                    var uri = new Uri(uri1, fname);
                    Queue.Add(uri.ToString());
                    Console.WriteLine($"#{crow}: '{fname}' {type} queued for download. (" + uri.ToString() + ")");
                }
            }
            Console.WriteLine("Queueing Completed.");
            DoneQueueing = true;
        }

        public static async Task DownloadThread()
        {
            await Task.Run(() =>
            {
                string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
                var abs = Path.GetFullPath(OutputDirectory);
                if (!Directory.Exists(abs)) Directory.CreateDirectory(abs);
                int num = 0;
                using(WebClient client = new WebClient())
                    while (true)
                    {
                        if (Queue.Count == 0 && DoneQueueing) break;
                        if (Queue.Count == 0) continue;
                        var current = Queue[0];
                        var fnames = current.Split('/');
                        var fname = fnames[fnames.Length - 1];
                        foreach (char c in invalid)
                        {
                            fname = fname.Replace(c.ToString(), "_"); 
                        }
                        var destname = Path.Combine(abs, fname);
                        
                        client.DownloadFile(current, destname);
                        num++;
                        if(DoneQueueing) Console.WriteLine("DL Progression: " + num + "/" + (num + Queue.Count));
                        Queue.RemoveAt(0);
                    }
            });
        }
    }
}