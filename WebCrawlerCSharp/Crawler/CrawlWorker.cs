using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Supremes.Nodes;
using Supremes;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using WebCrawlerCSharp.DataBase;

namespace WebCrawlerCSharp.Crawler {

    class CrawlWorker {
        private string threadName;
        private string startURL;
        private int workingDepth;
        private int startDepth;
        private int pagesCrawled;
        private int iteratorLocation;
        private int id;
        private bool sidedness;
        private WebStringUtils webStringUtils;
        private DownloadManager downloadManager;
        public string workingURL;
        public bool isFinished;
        private CrawlStruct data;
        /**
         * Threaded worker for scanning webpages. Automatically creates other copies
         * of itself and divides work for quicker scanning.
         *
         * @param threadName the name of the worker
         * @param urlTrie the trie containing visited pages
         * @param url the url to target
         * @param maxDepth the maximum depth of the search
         * @param startDepth the start depth of this worker
         * @param maxBreadth the maximum breadth of the search
         * @param sidedness the side this worker operates on (false = left, true =
         * right)
         * @param backCrawl enables backcrawling to find hidden pages
         * @param crawlDomain the domain the search is operating within
         */
        public CrawlWorker(int id, string url, int startDepth, bool sidedness, CrawlStruct data) {
            this.id = id;
            this.startURL = url;
            this.workingURL = url;
            this.sidedness = sidedness;
            this.startDepth = startDepth;
            this.workingDepth = startDepth;
            this.data = data;

            if (data.iterative) {
                this.workingURL += data.iteratorStart;
            }

            downloadManager = new DownloadManager(data);
            //The depth at which the other crawler is assisting

            this.pagesCrawled = 0;

            webStringUtils = new WebStringUtils(data.outputFolder);
            //Constructs string name from id and sidedness
            StringBuilder nameBuilder = new StringBuilder("Worker ").Append(id);
            if (!sidedness) {
                nameBuilder.Append(" (Left)");
            } else {
                nameBuilder.Append(" (Right)");
            }
            this.threadName = nameBuilder.ToString();
            Console.WriteLine("Creating " + threadName);
        }

        public int Run() {
            Console.WriteLine(threadName + " worker started!");
            try {
                //Executes iterative scan
                if (data.iterative) {
                    for (int i = 0; i < data.maxDepth; i++) {
                        iteratorLocation++;
                        //Mod this into a more-than-2-thread operation later
                        if (sidedness) {
                            while (iteratorLocation % 2 == 0) {
                                iteratorLocation++;
                            }
                        } else {
                            while (iteratorLocation % 2 != 0) {
                                iteratorLocation++;
                            }
                        }
                        pagesCrawled += Crawl(startURL + iteratorLocation, 0, sidedness);
                    }
                } else {
                    pagesCrawled = Crawl(workingURL, 0, sidedness);
                }
            } catch (Exception e) {
                CU.WCol(CU.nl + "something went terribly wrong, crawler died  " + e, CU.r, CU.y);
            }
            isFinished = true;
            Console.WriteLine(threadName + " finished");
            return (pagesCrawled);
        }

        public void createHelper() {
            CrawlWorker newHelper = new CrawlWorker((id + 1), workingURL, workingDepth, !sidedness, data);
            WebCrawler.SpawnCrawler(newHelper);
            //WebCrawler.SpawnCrawler(newHelper);
        }

        //false = left, true = right
        //This should probably be split into two or three classes that inherit
        public int Crawl(string url, int currentDepth, bool sidedness) {
            //helpers at their base level do now download content
            bool isHelper = false;
            //Iterative searchers spawn recursors to assist
            bool isIterativeRecursor = false;
            if (data.iterative && !url.Substring(0, url.LastIndexOf('/') + 1).Equals(data.startURL))
                isIterativeRecursor = true;

            int newPages = 0;

            //Early return if url is not in the domain or if it has been previously checked
            if (!url.Equals(startURL)) {
                //Checks to see if the URL has already been searched or if it's not in the domain- if so, terminate.
                if (!Regex.IsMatch(url, data.crawlPattern) || !Regex.IsMatch(url, data.crawlDomain) || data.urlTrie.contains(url)) {
                    return newPages;
                }
                //Bounces from DB 
                if (data.dataBaseCheck) {
                    Task<bool> isInDB = Task.Run(() => TagDBDriver.entryExists(url));
                    isInDB.Wait();
                    data.urlTrie.InsertURL(url);
                    if (isInDB.Result) {
                        return newPages;
                    }
                }
                newPages++;
            } else if (url.Equals(startURL)) {
                if (data.urlTrie.contains(url)) {
                    isHelper = true;
                    //isIterativeRecursor = true;
                }
            }

            //Iterative helpers still check the URLTrie

            if (isIterativeRecursor && (data.urlTrie.contains(url) || !Regex.IsMatch(url, data.crawlPattern)))
                return newPages;

            data.urlTrie.InsertURL(url);


            //Courtesy delaying
            Thread.Sleep(data.delay);

            if (data.verbose) {
                Console.WriteLine("Starting crawl on " + url);
            }

            Document HTMLDoc = null;
            int errorIterator = 0;
            while (HTMLDoc == null) {
                try {
                    HTMLDoc = getHTML(url);
                } catch (Exception exception) {
                    //Helpers do not increase the iterator
                    if (data.iterative && !isIterativeRecursor) {
                        if (exception.ToString().Contains("404")) {
                            iteratorLocation++;
                            Console.WriteLine("404. Now on page " + iteratorLocation + ". Increasing index....");
                        } else if (exception.ToString().Contains("503")) {
                            iteratorLocation++;
                            Console.WriteLine("503. Now on page " + iteratorLocation + ". Increasing index....");
                        } else
                            throw exception;
                    }

                    if (exception.ToString().Contains("429")) {
                        //Handling for rate limit exceptions
                        errorIterator++;
                        if (errorIterator < 1) {
                            Console.WriteLine(exception);
                            Console.WriteLine("Rate limited. waiting...");
                            Thread.Sleep(15000 + data.delay);
                            Console.WriteLine("Retrying...");
                        } else {
                            Console.WriteLine("Continued rate limiting. Thread waiting for one minute and increasing courtesy delay.");
                            Thread.Sleep(60000);
                            data.delay += 10000;
                            errorIterator = 0;
                        }
                    } else {
                        CU.WCol(CU.nl + "Could not load page. " + url + " : " + exception.Message + CU.nl, CU.r);
                        return newPages;
                    }
                }
            }
            //Grab links
            Elements links = HTMLDoc.Select("a[href]");
            int numberOfLinks = links.Count();
            //Grabs the page title
            string titleString = HTMLDoc.Title;
            if (titleString != null) {
                titleString = HTMLDoc.Title;
            } else {
                titleString = "Untitled";
            }
            if (!data.verbose) {
                if (!isHelper)
                    Console.WriteLine(threadName + " Crawling " + url.Truncate(40) + "(" + titleString.Truncate(40) + ")");
                else
                    Console.WriteLine(threadName + " Finishing " + url.Truncate(40) + "(" + titleString.Truncate(40) + ")");
            }
            if (data.verbose) {
                Console.WriteLine("Page name: " + titleString);
            }
            //Writes content to file
            try {
                //Refuse files if crawler is helper at level
                if (!isHelper) {
                    //Prep information for DB entries
                    FileInfo[] files = null;
                    string[] tags = null;
                    //Download HTML
                    if (data.downloadHTML) {
                        Thread downloadHTML = new Thread(() => downloadManager.downloadHTML(HTMLDoc));
                        downloadHTML.Start();
                    }
                    //Download text within specified tags (-dt [tag])
                    if (data.downloadText) {
                        Elements text = HTMLDoc.Select(data.textTag);
                        Task<string[]> downloadText = new Task<string[]>(() => downloadManager.downloadText(text));
                        downloadText.Start();
                        tags = downloadText.Result;
                    }
                    //Download images and links to images
                    if (data.downloadImages) {
                        //Checks for links to images
                        Elements imageElements = HTMLDoc.Select("img");
                        if (imageElements != null) {
                            //Append links to images as well
                            foreach (Element element in imageElements)
                                if (Regex.IsMatch(element.AbsUrl("href"), ".*(.jpg|.png|.gif|.webm)")) {
                                    imageElements.Add(element);
                                }
                        }
                        Task<FileInfo[]> downloadImages = new Task<FileInfo[]>(() => downloadManager.DownloadElementsReturnNames(imageElements));
                        downloadImages.Start();
                        files = downloadImages.Result;
                    }
                    //Saves image locations to Database
                    if (data.dataBaseImages) {
                        foreach (FileInfo file in files) {
                            new Thread(() => TagDBDriver.insertImageWithTags(file.FullName, tags)).Start();
                        }
                    }
                    //Saves links to Database
                    if (data.dataBaseLinks) {
                        new Thread(() => TagDBDriver.insertImageWithTags(url, tags)).Start();
                    }
                }
            } catch (Exception e) {
                Console.WriteLine("Could not write to file: " + e);
            }

            //Checks if the search needs to recurse
            if (numberOfLinks <= 0) {
                if (data.verbose) {
                    Console.WriteLine("No links on page. Going back up...");
                }
                return newPages;
            }
            //if the crawl is iterative, do not recurse
            try {
                //Recurses the algorithm if not at max depth
                if (currentDepth + 1 > data.maxDepth && !data.iterative)
                    return newPages;
                //Do shallow recursion while in iterative mode
                else if (currentDepth + 1 >= data.iterativeDepth && isIterativeRecursor)
                    return newPages;

                if (numberOfLinks > data.linkAssist && !data.assistTrie.contains(url) && !data.iterative) {
                    data.assistTrie.InsertURL(url);
                    this.workingURL = url;
                    this.workingDepth = currentDepth;
                    createHelper();
                }

                //Right-handed search
                //can these be one method?
                int sizeLimit = (int)Math.Round(numberOfLinks / 2f);
                if (sidedness) {
                    for (int i = numberOfLinks - 1; i > 0; i--) {
                        //Only search half at the entry depth
                        if (currentDepth == startDepth && i < sizeLimit) {
                            break;
                        }
                        string currentLinkRight = links[i].AbsUrl("href");
                        //Checks to make sure that the URL isn't a page in-reference and that it doesn't link to another part of the page. Also ensures link validity.
                        //Also ignore links to other pages positioned along the iterative crawl
                        if (string.IsNullOrEmpty(currentLinkRight) || currentLinkRight.Equals(url)
                            || (data.iterative && currentLinkRight.Substring(0, currentLinkRight.LastIndexOf('/') + 1).Equals(data.startURL))
                            || (currentLinkRight.Contains('#') && currentLinkRight.Substring(0, currentLinkRight.LastIndexOf('#')).Equals(url))) {
                            {
                                i--;
                                continue;
                            }
                        }
                        //Ensures the link can be connect to- if not, iterate to the next link
                        try {
                            WebRequest.Create(currentLinkRight);
                        } catch (Exception) {
                            i--;
                            continue;
                        }
                        newPages += Crawl(currentLinkRight, currentDepth + 1, sidedness);
                    }
                } //Left-handed search
                else {
                    for (int i = 0; i < numberOfLinks - 1; i++) {
                        if (currentDepth == startDepth && i > sizeLimit) {
                            break;
                        }
                        string currentLinkLeft = links[i].AbsUrl("href");
                        string test = currentLinkLeft.Substring(0, currentLinkLeft.LastIndexOf('/') + 1);
                        if (string.IsNullOrEmpty(currentLinkLeft) || currentLinkLeft.Equals(url)
                            || (data.iterative && currentLinkLeft.Substring(0, currentLinkLeft.LastIndexOf('/') + 1).Equals(data.startURL))
                            || (currentLinkLeft.Contains('#') && currentLinkLeft.Substring(0, currentLinkLeft.LastIndexOf('#')).Equals(url))) {
                            i++;
                            continue;
                        }
                        try {
                            WebRequest.Create(currentLinkLeft);
                        } catch (Exception) {
                            i++;
                            continue;
                        }
                        newPages += Crawl(currentLinkLeft, currentDepth + 1, sidedness);
                    }
                }


                //Backcrawl to hit missed directoies at the level 
                if (data.backCrawl) {
                    while (url.Substring(8).Contains("/")) {
                        Console.WriteLine("Backcrawling unfound urls...");
                        Crawl(url = url.Substring(0, url.LastIndexOf('/') - 1), currentDepth - 1, sidedness);
                    }
                }
            } catch (Exception e) {
                CU.WCol(CU.nl + "Dead page: " + e, CU.r, CU.y);
                CU.WCol(CU.nl + e.StackTrace, CU.r, CU.y);
                Console.WriteLine("Now checking depth " + currentDepth + ", link: " + url);
            }
            return newPages;
        }

        public Document getHTML(string urlString) {
            HttpWebRequest HTMLRequest = (HttpWebRequest)WebRequest.Create(urlString);
            HTMLRequest.UserAgent = "Mozilla/5.0 (Windows; U; WindowsNT 5.1; en-US; rv1.8.1.6) Gecko/20070725 Firefox/2.0.0.6";
            HTMLRequest.Referer = "http://google.com";
            HttpWebResponse HTMLResponse = (HttpWebResponse)HTMLRequest.GetResponse();
            Stream streamResponse = HTMLResponse.GetResponseStream();
            StreamReader streamRead = new StreamReader(streamResponse);
            //Sets the base URI of the page
            Document returnedDoc = Dcsoup.Parse(streamRead.ReadToEnd());
            returnedDoc.BaseUri = HTMLResponse.ResponseUri.ToString();
            return returnedDoc;
        }

        public int getPagesCrawled() {
            return pagesCrawled;
        }
    }
}