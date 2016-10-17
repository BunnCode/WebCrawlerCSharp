using System;
using System.Collections.Generic;
using System.Text;
using Supremes.Nodes;
using System.IO;
using System.Text.RegularExpressions;
using System.Net;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Collections;
using System.Threading.Tasks;
using WebCrawlerCSharp.WebCrawler.ImageDisplay;

namespace WebCrawlerCSharp.Crawler {
    public class DownloadManager {

        private WebStringUtils webStringUtils;
        private static CrawlStruct data;
        private SaveQueue saveQueue;

        public DownloadManager(CrawlStruct data) {
            DownloadManager.data = data;
            webStringUtils = new WebStringUtils(data.outputFolder);
            saveQueue = new SaveQueue();
        }

        public void downloadHTML(Document HTMLDoc) {
            string HTMLFile;
            string url = HTMLDoc.BaseUri;
            if (data.gallery) {
                HTMLFile = data.outputFolder + webStringUtils.UnFuck(HTMLDoc.Title) + ".html";
            } else {
                HTMLFile = webStringUtils.UrlToDir(url) + webStringUtils.UnFuck(HTMLDoc.Title) + ".html";
            }
            FileInfo file = new FileInfo(HTMLFile);
            MemoryStream textStream = new MemoryStream(Encoding.Unicode.GetBytes(HTMLDoc.ToString()));
            Save(textStream, file);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public string[] downloadText(Elements elements) {
            ArrayList tags = new ArrayList();
            if (elements != null) {
                foreach (Element content in elements) {
                    StringBuilder textBuilder = new StringBuilder();
                    string absURL = content.BaseUri;
                    if ((!data.textPattern.Equals("") && !Regex.IsMatch(absURL, data.textPattern))) {
                        break;
                    }
                    if (data.includeLinks) {
                        textBuilder.Append(absURL);
                    }
                    textBuilder.Append(content.Text);
                    string docText = textBuilder.ToString();
                    string textFile;
                    if (data.gallery) {
                        textFile = data.outputFolder + "textDigest" + ".txt";
                    } else {
                        textFile = webStringUtils.UrlToDir(absURL) + webStringUtils.UnFuck(docText.Substring(0, 10)) + ".txt";
                    }
                    FileInfo file = new FileInfo(textFile);
                    MemoryStream textStream = new MemoryStream(Encoding.Unicode.GetBytes(docText));
                    Save(textStream, file);
                    //Adds 
                    string[] newTags = docText.Split(' ');
                    foreach (string tag in newTags)
                        tags.Add(tag);
                }
            }
            string[] returnValue = (string[])tags.ToArray(typeof(string));
            return returnValue;
        }

        //Returns element names, and then saves
        public FileInfo[] DownloadElementsReturnNames(Elements elements) {
            ArrayList returnedElements = new ArrayList();
            ArrayList fileLocations = new ArrayList();
            foreach (Element content in elements) {

                string absURL;
                string tag = content.Tag.ToString();
                switch (tag) {
                    case "img":
                        absURL = content.AbsUrl("src");
                        break;
                    case "a":
                        absURL = content.AbsUrl("href");
                        break;
                    default:
                        absURL = content.AbsUrl("src");
                        break;
                }
                //Checks to ensure that the name matches the image pattern (if present) and that it is not in the trie
                if ((!data.imagePattern.Equals("") && !Regex.IsMatch(absURL, data.imagePattern)) || data.mediaTrie.contains(absURL)) {
                    continue;
                }

                int nameIndex = absURL.LastIndexOf('/');
                //Name of the element
                string elementName = Regex.Replace(absURL.Substring(nameIndex + 1), "[^A-Za-z.]", "");
                //File location of the element
                string elementLocation = absURL.Substring(0, nameIndex);
                if (elementName.Length > 20)
                    elementName = elementName.Substring(elementName.Length - 20);
                //Inserts hash into filename to avoid duplicates
                string hashCode = Convert.ToString(content.GetHashCode());
                elementName = elementName.Insert(0, hashCode);
                FileInfo file;
                if (!data.gallery)
                    file = new FileInfo(webStringUtils.UrlToDir(elementLocation) + elementName);
                else
                    file = new FileInfo(data.outputFolder + elementName);
                fileLocations.Add(file);
                returnedElements.Add(content);
            }
            FileInfo[] returnedInfo = (FileInfo[])fileLocations.ToArray(typeof(FileInfo));
            new Thread(() => DownloadElements((Element[])returnedElements.ToArray(typeof(Element)), returnedInfo)).Start();
            return returnedInfo;
        }
        //Saves all elements in argument
        public void DownloadElements(Element[] elements, FileInfo[] fileInfo = null) {
            int totalDownloaded = 0;
            int totalBounced = 0;
            //Returns if there are no files to be downloaded
            if (fileInfo != null && fileInfo.Length == 0)
                return;

            for (int i = 0; i < elements.Length; i++) {
                Element content = elements[i];

                string absURL;

                string tag = content.Tag.ToString();

                switch (tag) {
                    case "img":
                        absURL = content.AbsUrl("src");
                        break;
                    case "a":
                        absURL = content.AbsUrl("href");
                        break;
                    default:
                        absURL = content.AbsUrl("src");
                        break;
                }
                data.mediaTrie.InsertURL(absURL);
                FileInfo file;
                //Doesn't recaculate file info if it doesn't have to
                if (fileInfo == null) {

                    int nameIndex = absURL.LastIndexOf('/');
                    //Name of the element
                    string elementName = Regex.Replace(absURL.Substring(nameIndex + 1), "[^A-Za-z.]", "");
                    //File location of the element
                    string elementLocation = absURL.Substring(0, nameIndex);
                    if (elementName.Length > 20)
                        elementName = elementName.Substring(elementName.Length - 20);
                    //Inserts hash into filename to avoid duplicates
                    string hashCode = Convert.ToString(content.GetHashCode());
                    elementName = elementName.Insert(0, hashCode);
                    if (!data.gallery)
                        file = new FileInfo(webStringUtils.UrlToDir(elementLocation) + elementName);
                    else
                        file = new FileInfo(data.outputFolder + elementName);
                } else {
                    file = fileInfo[i];
                }


                //Defers downloading to the saver
                Save(absURL, file);
                //Sleeps to slow down image requests 
                Thread.Sleep(data.delay);
                totalDownloaded++;
            }
            string report = "Downloaded " + totalDownloaded + " media files, denied " + totalBounced;
            CU.WCol(CU.nl + report + CU.nl, CU.c);
        }


        private void Save(Stream stream, FileInfo fileInfo) {
            FileMode fileMode;
            if (data.gallery)
                fileMode = FileMode.Append;
            else
                fileMode = FileMode.CreateNew;
            //locks the filestream
            saveQueue.AddToQueue(new QueuedFile(stream, fileInfo, fileMode));
        }

        private void Save(string url, FileInfo fileInfo) {
            FileMode fileMode;
            if (data.gallery)
                fileMode = FileMode.Append;
            else
                fileMode = FileMode.CreateNew;
            saveQueue.AddToQueue(new QueuedFile(url, fileInfo, fileMode));
        }

    }

    //Handles saving to disk
    class SaveQueue {
        private static Queue<QueuedFile> queueStack = new Queue<QueuedFile>();
        private static FileStream fileStream;
        private static Thread saveThread;
        private static bool shouldAnHero;
        private const int MAXCONSECUTIVEDOWNLOADS = 10;
        private static WebClient testClient;
        private static LimitedConcurrencyLevelTaskScheduler downloadThreadExecutor;
        private static TaskFactory downloadThreadFactory;

        static SaveQueue() {
            saveThread = new Thread(() => SaveFiles());
            downloadThreadExecutor = new LimitedConcurrencyLevelTaskScheduler(MAXCONSECUTIVEDOWNLOADS);
            downloadThreadFactory = new TaskFactory(downloadThreadExecutor);
            saveThread.Start();
        }
        //Adds files to the queue
        public void AddToQueue(QueuedFile newFile) {
            queueStack.Enqueue(newFile);
        }

        private static async void SaveFiles() {
            Queue<Task> downloadTasks = new Queue<Task>();

            while (true) {
                if (queueStack.Count > 0) {
                    QueuedFile nextFile = queueStack.Dequeue();
                    if (nextFile.fileInfo == null)
                        continue;
                    //Checks if the next file contains URL info
                    if (nextFile.url != null) {
                        Task newDownload = downloadThreadFactory.StartNew(() => downloadFile(nextFile));
                        downloadTasks.Enqueue(newDownload);
                    } else {
                        saveFile(nextFile);
                        //Print image preview to console
                    }
                }
                while (downloadTasks.Count > 0) {
                    Task finishedTask = downloadTasks.Dequeue();
                    finishedTask.Wait();
                }
                //Ded
                if (shouldAnHero)
                    return;

            }
        }

        //Allows for concurrent downloading of files
        //Allows for concurrent downloading of files
        private static async void downloadFile(QueuedFile nextFile) {
            testClient = new WebClient();
            testClient.Proxy = GlobalProxySelection.GetEmptyWebProxy();
            try {
                /**Stream stream = null;
                HttpWebRequest ElementRequest = (HttpWebRequest)WebRequest.Create(nextFile.url);
                ElementRequest.UserAgent = "Mozilla/5.0 (Windows; U; WindowsNT 5.1; en-US; rv1.8.1.6) Gecko/20070725 Firefox/2.0.0.6";
                ElementRequest.Referer = "http://google.com";
                HttpWebResponse HTMLResponse = (HttpWebResponse)ElementRequest.GetResponse();
                Stream streamResponse = HTMLResponse.GetResponseStream();
                //Downloads the image and saves it to the memorystream
                stream = HTMLResponse.GetResponseStream();
                nextFile.stream = stream;**/
                //Attempting to fix 403 error on many fullsize image loads
                byte[] testBytes = await testClient.DownloadDataTaskAsync(new Uri(nextFile.url));
                nextFile.stream = new MemoryStream(testBytes);
                //ImageDisplay.printImage(stream);
                saveFile(nextFile);
            } catch (Exception e) {
                CU.WCol(CU.nl + "Could not download image at url " + nextFile.url + " : " + e + CU.nl, CU.r, CU.y);
                CU.WCol(CU.nl + e.StackTrace + CU.nl, CU.r, CU.y);
            }
        }

        public static void saveFile(QueuedFile nextFile) {
            using (fileStream = new FileStream(nextFile.fileInfo.FullName, nextFile.fileMode)) {
                nextFile.stream.CopyTo(fileStream);
                fileStream.Flush();
                nextFile.stream.Flush();
                //CU.WCol(CU.nl + "Saved " + nextFile.fileInfo.FullName + CU.nl, CU.c);
            }
        }

        public static void killService() {
            // shouldAnHero = true;
        }
    }
    //File to save
    struct QueuedFile {
        public Stream stream { get; set; }
        public string url { get; }
        public FileInfo fileInfo { get; }
        public FileMode fileMode { get; }
        //Queue file from memorystream
        public QueuedFile(Stream memoryStream, FileInfo fileInfo, FileMode fileMode) {
            this.stream = memoryStream;
            this.fileInfo = fileInfo;
            this.fileMode = fileMode;
            this.url = null;
        }
        //Queue file from URL
        public QueuedFile(string url, FileInfo fileInfo, FileMode fileMode) {
            this.url = url;
            this.fileInfo = fileInfo;
            this.fileMode = fileMode;
            this.stream = null;
        }
    }
}