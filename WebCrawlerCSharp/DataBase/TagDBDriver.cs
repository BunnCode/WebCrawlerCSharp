using System;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Threading.Tasks;
using WebCrawlerCSharp.Crawler;

namespace WebCrawlerCSharp.DataBase {
    class TagDBDriver {
        protected static IMongoClient client;
        protected static IMongoDatabase database;
        protected static IMongoCollection<BsonDocument> collection;

        public static void instantiateDB(string ip) {
            MongoClientSettings settings = new MongoClientSettings();
            settings.Server = new MongoServerAddress(ip, 27017);
            client = new MongoClient(settings);
            database = client.GetDatabase("imageTags");
            collection = database.GetCollection<BsonDocument>("taggedImages");
        }
        //Asynchronous adding to db
        public static async void insertImageWithTags(string file, string[] tags) {
            var imageEntry = new BsonDocument{
                { "name", file }
            };
            //Array of new tags
            BsonArray entryTagArray = new BsonArray();
            foreach (string tag in tags) {
                entryTagArray.Add(new BsonDocument {
                        { "tag" , tag }
                });
            }
            imageEntry.Add("tags", entryTagArray);
            //Inserts tagged image into database
            await collection.InsertOneAsync(imageEntry);
            //CU.WCol(CU.nl + "Inserted " + imageEntry.ElementCount + " tags into the db " + CU.nl, CU.g);
        }
        //Returns the filenames of images with a given tag
        public static async Task<string[]> getImagesWithTag(string tag) {
            var filter = Builders<BsonDocument>.Filter.Eq("tags.tag", tag);
            var result = await collection.Find(filter).ToListAsync();
            string[] returnedTags = new string[result.Count];
            for (int i = 0; i < result.Count; i++)
                returnedTags[i] = result[i].GetElement("tag").Value.ToString();
            return returnedTags;
        }

        //Returns true if the name already exists in the Database
        public static async Task<bool> entryExists(string name) {
            var filter = Builders<BsonDocument>.Filter.Eq("name", name);
            try {
                var result = await collection.Find(filter).FirstAsync();
                return true;
            } catch (Exception) {
                return false;
            }
        }
    }
}

