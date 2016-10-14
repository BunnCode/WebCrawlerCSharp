# WebCrawlerCSharp
C# Web Crawler and Data Aggregator- Aggregates data and saves it to a DB. 

If you wish to simply run the existing windows binary, the current release is located in WebCrawlerCSHarp/bin/relase. Remember, being a command line tool, you need to run it from a command line if you want readable output.

Following is the printout from the current -h command, it should give you an idea of what functionality to expect in the current version.

```
Required tags for their category are in red and recommended tags are in yellow

Enter the URL followed by tags. Even if you aren't doing a scan, you need a URL

Help options:
-h, --help                  display this help dialog
-ph,--pattern help          help with regex patterns for search limiting

Crawl Options:
-bc,--backcrawl             deep copy, enables discovery of hidden pages (slow)
-c,--courtesy <int>         delay between page loads, in milliseconds
-d,--depth <int>            depth of the search (default 10)
-p,--pattern <regex>        regex pattern for restricting pages
-t,--threads <int>          number of allowed threads. More threads = more aggressive (must be 2+)
-ul,--unlock                unlocks crawler from target domain
-i,--iterative <int>        scans urls iteratively in the form of url/1,2.. starting at <param>
-id,--iterative depth <int> Depth to scan to at each step of the iteration
-dc,--dont crawl            do not execute crawl. (for the purpose of using other utilities only)

File Options:
-di,--images <regex>        download images while crawling (takes regex for filtering)
-dt,--text <tag, regex>     download text bodies from <tag> for analyzation, if the page matches <regex>
-g,--gallery                only download files to one folder
-ddh,--HTML                 don't download HTML while crawling
-il,--include link          include links to the parent page in text files
-l,--load <filename>        load data from previous scan, named <filename>
-o,--output <dir>           output location (defaults to exe location)
-O,--overwrite              overwrite files when scan starts

Database Options:
-dbip,--database ip <ip>    the IP address of the database to dump to
-dbc,--database check       Check the database to prevent duplicate entries (Slow and expensive)
-dbi,--database images      Save image locations to database, with tags defined by -dt
-dbl,--database links       Save visited links into the DB, with tags defined by -dt

Data processing Options:
-m,--markov <int>           generate a markov chain of <int> prefix Length and saves it.
-mp,--print markov <int>    prints out <int> sentences from the chain (Must use -g)

Output Options:
-v,--verbose                verbose mode
-vi,--visited               print visited pages a after completion (n.i.);

Example usages:
Basic scan: java -jar [JARNAME] http://pornhub.com -di -d 5
Site Image Gallery: [URL] -di -ddh -g
Fullsize gallery of 4chan thread: [URL] -di ^((?!s.).)*$ -ddh -g -p .*/((?!#[spq]).)*
Sankaku tags on posts with urls: [URL] -g -il -ddh -dt title (.*)(/post/show/)(.*) -O -c 1000 -d 3
Iterative booru tag crawl: [BASEURL] -g -il -ddh -dt title -O -c 1000 -d 1000 -i <startpage>
Markov chain from 4chan board: [URL] -t 10 -d 15 -dt .post .* -m 2 -g -ddh -O -mp 40
Insert images into database with tags: [BOORUURL] -g -t 10 -di .*[/](_images/).* -ddh -d 10 -O -p .*[/]post/.* -ul -dt title -dbi)
```
