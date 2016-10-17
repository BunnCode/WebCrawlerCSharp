using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using net.sf.dotnetcli;
using DataStructures.SimpleTrie;
using DataAnalyzation.Markov.DataAnalyzation;
using System.Text.RegularExpressions;
using System.IO;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using WebCrawlerCSharp.DataBase;
using System.Runtime.InteropServices;

namespace WebCrawlerCSharp.Crawler {

    public struct CrawlStruct {
        public string crawlDomain, crawlPattern, imagePattern, textTag, textPattern, startURL, outputFolder;
        public WebTrie urlTrie, mediaTrie, assistTrie;
        public int maxDepth, iteratorStart, iterativeDepth;
        public bool backCrawl, gallery, verbose, downloadImages, downloadHTML, downloadText, includeLinks, iterative, overwrite, dataBaseImages, dataBaseLinks, dataBaseCheck;
        public int linkAssist;
        //Delay can be modified during the scan but it is still thread-independant 
        public int delay;
    }

    class WebCrawler {
        private const float SAVERATE = 1f;

        private static ObjSaveUtils objSaveUtils;
        private static WebStringUtils webStringUtils;
        private static bool shouldCrawl, crawlLocked, printMarkov, loadMarkov, loadVisited;
        private static int pagesCrawled, markovLength, markovSentences, timesSaved;
        private static string saveFile;
        private static MarkovChain markovChain;
        private static LimitedConcurrencyLevelTaskScheduler crawlThreadExecutor;
        private static TaskFactory crawlThreadFactory;
        private static Queue<Task<int>> crawlTasks = new Queue<Task<int>>();
        private static CrawlStruct data = new CrawlStruct();

        static void Main(string[] args) {
            //Handles early exits
            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
            //Loads embedded DLLs into EXE if they don't load automatically
            AppDomain.CurrentDomain.AssemblyResolve += (sender, arguments) => {
                string resourceName = "AssemblyLoadingAndReflection." +
                    new AssemblyName(arguments.Name).Name + ".dll";
                using (var stream = Assembly.GetExecutingAssembly()
                                            .GetManifestResourceStream(resourceName)) {
                    byte[] assemblyData = new byte[stream.Length];
                    stream.Read(assemblyData, 0, assemblyData.Length);
                    return Assembly.Load(assemblyData);
                }
            };

            //Application initialization

            webStringUtils = new WebStringUtils(getAppFolder());
            objSaveUtils = new ObjSaveUtils(getAppFolder() + '/');
            //Help
            //Options options = new Options();
            Options options = new Options();
            options.AddOption(new Option("h", "help", false, "display this help dialog"));
            options.AddOption(new Option("ph", "pattern help", false, "help with the -p command"));
            options.AddOption(new Option("v", "verbose", false, "verbose mode"));
            //options.AddOptionGroup(helpOptions);
            //Crawl options
            options.AddOption(new Option("dc", "dont crawl", false, "do not execute crawl. (for the purpose of using other utilities only)"));
            options.AddOption(new Option("vi", "visited", false, "print visited pages a after completion (n.i.)"));
            options.AddOption(new Option("ul", "unlock", false, "unlocks crawler from target domain"));
            options.AddOption(new Option("bc", "backcrawl", false, "deep copy, enables discovery of hidden pages (slow)"));
            options.AddOption(new Option("p", "pattern", true, "regex pattern for restricting pages"));
            options.AddOption(new Option("d", "depth", true, "depth of the search (default 10)"));
            options.AddOption(new Option("c", "courtesy", true, "delay between page loads, in milliseconds"));
            options.AddOption(new Option("t", "threads", true, "number of allowed threads. More threads = more aggressive (must be 2+)"));
            options.AddOption(new Option("i", "iterative", true, "scans urls iteratively in the form of url/1,2.. starting at <param>"));
            options.AddOption(new Option("id", "iterative depth", true, "Depth to scan to at each step of the iteration"));
            //File options
            options.AddOption(new Option("O", "overwrite", false, "overwrite files when scan starts"));
            Option downloadImages = new Option("di", "images", true, "download images while crawling (takes regex for filtering)");
            downloadImages.OptionalArg = true;
            downloadImages.NumberOfArgs = 1;
            options.AddOption(downloadImages);
            Option downloadText = new Option("dt", "text", false, "download text bodies for analyzation <tag, regex>");
            downloadText.OptionalArg = true;
            downloadText.NumberOfArgs = 2;
            downloadText.ValueSeparator = ' ';
            options.AddOption(downloadText);
            options.AddOption(new Option("il", "include link", false, "include links to the parent page in text files"));
            options.AddOption(new Option("g", "gallery", false, "only download files to one folder"));
            options.AddOption(new Option("o", "output", true, "output location (defaults to exe location)"));
            options.AddOption(new Option("l", "load", true, "load data from previous scan, named <param>"));
            //Database options
            options.AddOption(new Option("dbl", "database links", false, "Save visited links into the DB, with tags defined by -dt"));
            options.AddOption(new Option("dbi", "database images", false, "Save image locations to database, with tags defined by -dt"));
            options.AddOption(new Option("ddh", "HTML", false, "don't download HTML while crawling"));
            options.AddOption(new Option("dbc", "database check", false, "Check the database to prevent duplicate entries (slow)"));
            options.AddOption(new Option("dbip", "database ip", true, "the IP address of the database to dump to"));
            //Data processing
            options.AddOption(new Option("m", "markov", true, "generate a markov chain of <param> prefix Length and saves it."));
            options.AddOption(new Option("mp", "print markov", true, "prints out [param] sentences from the chain (Must use -g)"));
            //Attempts to parse args
            try {
                ICommandLineParser parser = new PosixParser();
                //Help options
                CommandLine helpCmd = parser.Parse(options, args);
                HelpFormatter helpFormatter = new HelpFormatter();
                helpFormatter.Width = 100;
                helpFormatter.DescPadding = 0x1;
                //string helpHeader = "\nSKS Web crawler/info extractor v0.1";
                string helpHeader = "\nSKS Web crawler/info extractor v0.1";
                string helpFooter = "\nExample Usage: java -jar [JARNAME] http://pornhub.com -di -d 5"
                        + "\nSite Image Gallery: [URL] -di -ddh -g"
                        + "\nFullsize gallery of 4chan thread: [URL] -di ^((?!s.).)*$ -ddh -g -p .*/((?!#[spq]).)*"
                        + "\nSankaku tags on posts with urls: [URL] -g -il -ddh -dt title (.*)(/post/show/)(.*) -O -c 1000 -d 3"
                        + "\nIterative booru tag crawl: [BASEURL] -g -il -ddh -dt title -O -c 1000 -d 1000 -i <startpage>"
                        + "\nMarkov chain from 4chan board: [URL] -t 10 -d 15 -dt .post .* -m 2 -g -ddh -O -mp 40"
                        + "\nInsert images into database with tags: [BOORUURL] -g -t 10 -di .*[/](_images/).* -ddh -d 10 -O -p .*[/]post/.* -ul -dt title -dbi";
                if (helpCmd.HasOption("ph")) {
                    Console.WriteLine("\n-p and -i take a regular exp. as an argument, searching all URLs"
                            + "\nthat match the pattern. I.E., \"test.com/page \" would "
                            + "\nmatch \"test.com/page/page2\". To test for any subdomain,"
                            + "\nthe following pattern would operate on [anything].test.com:"
                            + "\nhttps?://([^/.]+[.])*test.com(.*)");
                    return;
                }
                data.verbose = helpCmd.HasOption("v");
                //Crawl options
                CommandLine crawlCmd = parser.Parse(options, args);

                if (args.Length > 0) data.startURL = args[0];
                data.backCrawl = crawlCmd.HasOption("bc");
                data.iterative = crawlCmd.HasOption("i");
                shouldCrawl = !crawlCmd.HasOption("dc");
                data.iteratorStart = Convert.ToInt32(crawlCmd.GetOptionValue("i", "0"));
                data.iterativeDepth = Convert.ToInt32(crawlCmd.GetOptionValue("id", "0"));
                data.crawlPattern = crawlCmd.GetOptionValue("p", ".*");
                data.maxDepth = Convert.ToInt32(crawlCmd.GetOptionValue("d", "5"));
                data.delay = Convert.ToInt32(crawlCmd.GetOptionValue("c", "0"));
                crawlThreadExecutor = new LimitedConcurrencyLevelTaskScheduler(Convert.ToInt32(crawlCmd.GetOptionValue("t", "2")));
                crawlThreadFactory = new TaskFactory(crawlThreadExecutor);
                crawlLocked = !crawlCmd.HasOption("ul");

                //File options
                CommandLine fileCmd = parser.Parse(options, args);
                data.overwrite = fileCmd.HasOption("O");
                data.downloadImages = fileCmd.HasOption("di");
                data.imagePattern = fileCmd.GetOptionValue("di", "");
                data.downloadText = fileCmd.HasOption("dt");
                data.downloadHTML = !fileCmd.HasOption("ddh");
                data.gallery = fileCmd.HasOption("g");

                if (data.downloadText) {
                    string[] imageOptions = fileCmd.GetOptionValues("dt");
                    //textTag = cmd.GetOptionValue("dt", null);
                    data.textTag = imageOptions[0];
                    try {
                        data.textPattern = imageOptions[1];
                    } catch (Exception) {
                        data.textPattern = "";
                    }
                    data.includeLinks = fileCmd.HasOption("il");
                }
                if (fileCmd.HasOption("l")) {
                    saveFile = fileCmd.GetOptionValue("l");
                    //Loads the chain
                    if (fileCmd.HasOption("m"))
                        markovChain = (MarkovChain)objSaveUtils.LoadObject("markov_" + saveFile, typeof(MarkovChain));
                    //Loads the tries
                    data.urlTrie = (WebTrie)objSaveUtils.LoadObject("visitedTrie_" + saveFile, typeof(WebTrie));
                    data.assistTrie = (WebTrie)objSaveUtils.LoadObject("assistTrie_" + saveFile, typeof(WebTrie));
                    data.mediaTrie = (WebTrie)objSaveUtils.LoadObject("assistTrie_" + saveFile, typeof(WebTrie));
                } else {
                    if (args.Length > 0) saveFile = webStringUtils.UnFuck(args[0]);
                    //If not loading chain from file, create new chain
                    if (fileCmd.HasOption("m"))
                        markovChain = new MarkovChain(Convert.ToInt32(fileCmd.GetOptionValue("m", "3")));
                    //Attempts to automatically load file name
                    try {
                        data.urlTrie = (WebTrie)objSaveUtils.LoadObject("visitedTrie_" + saveFile, typeof(WebTrie));
                        data.assistTrie = (WebTrie)objSaveUtils.LoadObject("assistTrie_" + saveFile, typeof(WebTrie));
                        data.mediaTrie = (WebTrie)objSaveUtils.LoadObject("assistTrie_" + saveFile, typeof(WebTrie));
                    } catch (Exception) {
                        //Generate tries if not loadable
                        data.urlTrie = new WebTrie();
                        data.assistTrie = new WebTrie();
                        data.mediaTrie = new WebTrie();
                    }
                   
                }
                data.outputFolder = fileCmd.GetOptionValue("o", getAppFolder()) + "CrawlResults\\";

                //Database options
                CommandLine dbCmd = parser.Parse(options, args);
                if (dbCmd.HasOption("dbip")) {
                    TagDBDriver.instantiateDB(dbCmd.GetOptionValue("dbip"));
                    data.dataBaseImages = dbCmd.HasOption("dbi");
                    data.dataBaseLinks = dbCmd.HasOption("dbl");
                    data.dataBaseCheck = dbCmd.HasOption("dbc");
                }

                //Data processing options
                CommandLine dpCmd = parser.Parse(options, args);
                printMarkov = dpCmd.HasOption("mp");
                markovSentences = Convert.ToInt32(dpCmd.GetOptionValue("mp", "0"));

                if (helpCmd.HasOption("h") || args.Length == 0) {
                    printHelp();
                    return;
                }


            } catch (Exception exception) {
                Console.WriteLine("Invalid arguments or parameters. use -h for help (" + exception + ")");
                return;
            }
            //instantiates trie

            //creates regex for site locking
            if (crawlLocked) {
                string regexURL = Regex.Replace(args[0], "https?://", "");
                data.crawlDomain = "https?://([^/.]+[.])*" + regexURL + "(.*)";
            } else {
                data.crawlDomain = ".*";
            }

            try {
                Crawl(args[0], data);
            } catch (Exception e) {
                Console.WriteLine("Scan aborted: " + e);
            }
            // System.exit(0);
        }

        private static void printHelp() {
            //helpFormatter.PrintHelp("java -jar [JARNAME] [URL]", helpHeader, options, helpFooter, true);
            Console.WriteLine("SKS Web crawler/info extractor v0.1");
            Console.Write("Required tags for their category are in "); CU.WCol("red", CU.r); Console.Write(" and recommended tags are in "); CU.WCol("yellow", CU.y);
            Console.Write(CU.nl + CU.nl + "Enter the URL followed by tags. "); CU.WCol("Even if you aren't doing a scan, you need a URL", CU.r);
            Console.WriteLine();
            Console.Write(CU.nl + "Help options:");
            Console.Write(CU.nl + "-h, --help                  display this help dialog");
            Console.Write(CU.nl + "-ph,--pattern help          help with regex patterns for search limiting");
            Console.WriteLine();
            Console.Write(CU.nl + "Crawl Options:");
            Console.Write(CU.nl + "-bc,--backcrawl             deep copy, enables discovery of hidden pages (slow)");
            CU.WCol(CU.nl + "-c,--courtesy <int>", CU.y); Console.Write("         delay between page loads, in milliseconds");
            CU.WCol(CU.nl + "-d,--depth <int>", CU.y); Console.Write("            depth of the search (default 10)");
            CU.WCol(CU.nl + "-p,--pattern <regex>", CU.y); Console.Write("        regex pattern for restricting pages");
            CU.WCol(CU.nl + "-t,--threads <int>", CU.y); Console.Write("          number of allowed threads. More threads = more aggressive (must be 2+)");
            Console.Write(CU.nl + "-ul,--unlock                unlocks crawler from target domain");
            Console.Write(CU.nl + "-i,--iterative <int>        scans urls iteratively in the form of url/1,2.. starting at <param>");
            Console.Write(CU.nl + "-id,--iterative depth <int> Depth to scan to at each step of the iteration");
            Console.Write(CU.nl + "-dc,--dont crawl            do not execute crawl. (for the purpose of using other utilities only)");
            Console.WriteLine();
            Console.Write(CU.nl + "File Options:");
            Console.Write(CU.nl + "-di,--images <regex>        download images while crawling (takes regex for filtering)");
            Console.Write(CU.nl + "-dt,--text <tag, regex>     download text bodies from <tag> for analyzation, if the page matches <regex>");
            Console.Write(CU.nl + "-g,--gallery                only download files to one folder");
            Console.Write(CU.nl + "-ddh,--HTML                 don't download HTML while crawling");
            Console.Write(CU.nl + "-il,--include link          include links to the parent page in text files");
            //CU.WCol(CU.nl + "-l,--load <filename>", CU.y); Console.Write("        load data from previous scan, named <filename> ");
            CU.WCol(CU.nl + "-o,--output <dir>", CU.y); Console.Write("           output location (defaults to exe location)");
            Console.Write(CU.nl + "-O,--overwrite              overwrite files when scan starts");
            Console.WriteLine();
            Console.Write(CU.nl + "Database Options:");
            CU.WCol(CU.nl + "-dbip,--database ip <ip>", CU.r); Console.Write("    the IP address of the database to dump to");
            Console.Write(CU.nl + "-dbc,--database check       Check the database to prevent duplicate entries "); CU.WCol("(Slow and expensive)", CU.y);
            Console.Write(CU.nl + "-dbi,--database images      Save image locations to database, with tags defined by -dt");
            Console.Write(CU.nl + "-dbl,--database links       Save visited links into the DB, with tags defined by -dt");
            Console.WriteLine();
            Console.Write(CU.nl + "Data processing Options:");
            Console.Write(CU.nl + "-m,--markov <int>           generate a markov chain of <int> prefix Length and saves it.");
            Console.Write(CU.nl + "-mp,--print markov <int>    prints out <int> sentences from the chain (Must use -g)");
            Console.WriteLine();
            Console.Write(CU.nl + "Output Options:");
            Console.Write(CU.nl + "-v,--verbose                verbose mode");
            Console.Write(CU.nl + "-vi,--visited               print visited pages a after completion (n.i.);");
            Console.WriteLine();
            Console.Write(CU.nl + "Example usages:");
            Console.Write(CU.nl + "Basic scan: java -jar [JARNAME] http://examplesite.com -di -d 5");
            Console.Write(CU.nl + "Site Image Gallery: [URL] -di -ddh -g");
            Console.Write(CU.nl + "Fullsize gallery of 4chan thread: [URL] -di ^((?!s.).)*$ -ddh -g -p .*/((?!#[spq]).)*");
            Console.Write(CU.nl + "Booru tags on posts with urls: [URL] -g -il -ddh -dt title (.*)(/post/show/)(.*) -O -c 1000 -d 3");
            Console.Write(CU.nl + "Iterative booru tag crawl: [BASEURL] -g -il -ddh -dt title -O -c 1000 -d 1000 -i <startpage>");
            Console.Write(CU.nl + "Markov chain from 4chan board: [URL] -t 10 -d 15 -dt .post .* -m 2 -g -ddh -O -mp 40");
            Console.Write(CU.nl + "Insert images into database with tags: [BOORUURL] -g -t 10 -di .*[/](_images/).* -ddh -d 10 -O -p .*[/]post/.* -ul -dt title -dbi)" + CU.nl);   
        }

        private static void Crawl(string urlString, CrawlStruct data) {
            //Defaults to searching left side

            //Clears old crawl content 
            if (data.overwrite) {
                try {
                    Directory.Delete(data.outputFolder, true);
                    Console.WriteLine("Deleted old scan files.");
                    Directory.CreateDirectory(data.outputFolder);
                } catch (IOException exception) {
                    Directory.CreateDirectory(data.outputFolder);
                    Console.WriteLine("First scan- Not files to delete.");
                }

            }
            Directory.CreateDirectory(data.outputFolder);
            //Spawns the markov chain
            if (shouldCrawl) {
                if (data.iterative) {
                    CrawlWorker evenCrawl = new CrawlWorker(0, urlString, 0, false, data);
                    CrawlWorker oddCrawl = new CrawlWorker(0, urlString, 0, true, data);
                    SpawnCrawler(evenCrawl);
                    SpawnCrawler(oddCrawl);
                } else {
                    CrawlWorker crawlWorker = new CrawlWorker(0, urlString, 0, false, data);
                    SpawnCrawler(crawlWorker);
                }
                //Automatic saving
                Thread saveThread = new Thread(() => {
                    while (true) {
                        Thread.Sleep((int)TimeSpan.FromMinutes(SAVERATE).TotalMilliseconds);
                        saveScanState();
                    }
                });
                saveThread.IsBackground = true;
                saveThread.Start();
                //Checks pages crawled
                Thread titleThread = new Thread(() => {
                    while (true) {
                        Console.Title = ("Scanned " + pagesCrawled + " pages(infrequently updates), saved " + timesSaved + " backups of pages visited");
                        Thread.Sleep((int)TimeSpan.FromSeconds(1).TotalMilliseconds);
                    }
                });
                titleThread.IsBackground = true;
                titleThread.Start();
                //waits for the threads to complete
                Thread.Sleep(1000);
                while (true) {
                    while (crawlTasks.Count > 0) {
                        Task<int> finishedTask = crawlTasks.Dequeue();
                        pagesCrawled += finishedTask.Result;
                    }
                    SaveQueue.killService();
                    break;
                }
                //Starts chain generation
                Console.WriteLine("Done! Scanned " + pagesCrawled + " pages.");
            }
            if (printMarkov) {
                Console.WriteLine("Printing sentence from generated chain... ");
                markovChain.addWords(File.ReadAllText(data.outputFolder + "textDigest" + ".txt"));
                markovChain.generateSentence(markovSentences);
            }
            //Save scan results
            saveScanState();
        }

        //Spawns crawler helpers
        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void SpawnCrawler(CrawlWorker crawlWorker) {
            Task<int> newCrawler = crawlThreadFactory.StartNew(crawlWorker.Run);
            crawlTasks.Enqueue(newCrawler);
            //todo threading code
        }

        private static string getAppFolder() {
            try {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                //string baseDir = "/Documents/";
                return baseDir;
            } catch (Exception) {
                return "something broke";
            }
        }

        //Saves yalls shit before shutting down
        static void OnProcessExit(object sender, EventArgs e) {
            saveScanState();
        }

        static void saveScanState() {
            if (markovChain != null) {
                objSaveUtils.SaveObject("markov_" + saveFile, markovChain, true);
                //Other text processing functions here
            }
            objSaveUtils.SaveObject("visitedTrie_" + saveFile, data.urlTrie.Clone(), false);
            objSaveUtils.SaveObject("assistTrie_" + saveFile, data.assistTrie.Clone(), false);
            objSaveUtils.SaveObject("mediaTrie_" + saveFile, data.mediaTrie.Clone(), false);
            timesSaved++;
        }
    }

    class WebStringUtils {
        string baseDir;
        public WebStringUtils(string baseDir) {
            this.baseDir = baseDir;
        }

        //Takes a URL and returns a directory string. Additionally, creates the dir if it didn't exist yet.
        public string UrlToDir(string url) {
            StringBuilder pathBuilder = new StringBuilder(baseDir).Append(UnFuck(url));
            if (pathBuilder[pathBuilder.Length - 1] != '/') {
                pathBuilder.Append('/');
            }
            Directory.CreateDirectory(pathBuilder.ToString());
            return pathBuilder.ToString();
        }

        //format URLs for parsing as local paths
        public string UnFuck(string input) {
            //Remove http headers from URL
            string returnedString = Regex.Replace(input, "https?://", "");
            returnedString = Regex.Replace(returnedString, "[^A-Za-z0-9' '._]", "");
            return returnedString;
        }
    }

    public static class CU {
        public const ConsoleColor r = ConsoleColor.Red;
        public const ConsoleColor g = ConsoleColor.Green;
        public const ConsoleColor b = ConsoleColor.Blue;
        public const ConsoleColor gy = ConsoleColor.Gray;
        public const ConsoleColor y = ConsoleColor.Yellow;
        public const ConsoleColor c = ConsoleColor.Cyan;
        public static string nl = Environment.NewLine;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void WCol(string input, ConsoleColor color) {
            Console.ForegroundColor = color;
            Console.Write(input);
            Console.ResetColor();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void WCol(string input, ConsoleColor foreColor, ConsoleColor backColor) {
            Console.ForegroundColor = foreColor;
            Console.BackgroundColor = backColor;
            Console.Write(input);
            Console.ResetColor();
        }

        public static string Truncate(this string value, int maxLength) {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}

