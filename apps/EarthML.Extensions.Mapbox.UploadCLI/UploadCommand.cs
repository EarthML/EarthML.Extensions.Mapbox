using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Transfer;
using Microsoft.Extensions.Configuration;
using System;
using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace EarthML.Extensions.Mapbox.UploadCLI
{
    public class MapBoxCredResponse
    {
        public string Bucket { get; set; }
        public string Key { get; set; }
        public string AccessKeyId { get; set; }
        public string SessionToken { get; set; }
        public string SecretAccessKey { get; set; }
        public string Url { get; set; }

    }
    public class UploadCommand : Command
    {

        public Option<string> Token = new Option<string>("--token", "The path");
        public Option<string> Path = new Option<string>("--path", "The path");
        public Option<string> TileSet = new Option<string>("--tileset", "The tileset");
        public Option<string> NameOption = new Option<string>("--name", "The name");

        private readonly IConfiguration configuration;
        private readonly IHttpClientFactory httpClientFactory;

        public UploadCommand(IConfiguration configuration, IHttpClientFactory httpClientFactory) : base("upload", "Upload to Mapbox")
        {
            Add(Path);
            Add(TileSet);
            Add(NameOption);
            Add(Token);

            Handler = CommandHandler.Create<ParseResult, IConsole>(Run);
            this.configuration = configuration ?? throw new System.ArgumentNullException(nameof(configuration));
            this.httpClientFactory = httpClientFactory ?? throw new System.ArgumentNullException(nameof(httpClientFactory));
        }
        static void uploadRequest_UploadPartProgressEvent(object sender, UploadProgressArgs e)
        {
            // Process event.
            Console.Write("\r{0}/{1}", e.TransferredBytes, e.TotalBytes);
        }
        public async Task Run(ParseResult parseResult, IConsole console) //(string path, string customizationprefix)
        {
            var token = parseResult.GetValueForOption(Token) ?? configuration.GetValue<string>("MapBoxToken");
            var client = httpClientFactory.CreateClient();

            var credResponse = await client.PostAsync("https://api.mapbox.com/uploads/v1/kjeldager/credentials?access_token=" + token,new StringContent(""));

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var cred = JsonSerializer.Deserialize<MapBoxCredResponse>(await credResponse.Content.ReadAsStreamAsync(), options);

            var s3 = new AmazonS3Client(cred.AccessKeyId,cred.SecretAccessKey,cred.SessionToken,Amazon.RegionEndpoint.USEast1);

            var fileTransferUtility = new TransferUtility(s3);

            try
            {
                var uploadRequest = new TransferUtilityUploadRequest
                {
                    BucketName = cred.Bucket,
                    Key = cred.Key,
                    FilePath = parseResult.GetValueForOption(Path) //@"C:\projects\ghana\map.tif"
                     
                   // ContentType = "text/plain" 
                };


                uploadRequest.UploadProgressEvent +=
                   new EventHandler<UploadProgressArgs>
                       (uploadRequest_UploadPartProgressEvent);

                await fileTransferUtility.UploadAsync(uploadRequest);
                Console.WriteLine("Upload completed");

            }
            catch (AmazonS3Exception amazonS3Exception)
            {
                if (amazonS3Exception.ErrorCode != null &&
                    (amazonS3Exception.ErrorCode.Equals("InvalidAccessKeyId")
                    ||
                    amazonS3Exception.ErrorCode.Equals("InvalidSecurity")))
                {
                    throw new Exception("Check the provided AWS Credentials.");
                }
                else
                {
                    throw new Exception("Error occurred: " + amazonS3Exception.Message);
                }

            }

            var uploadResponse = await client.PostAsync("https://api.mapbox.com/uploads/v1/kjeldager?access_token=" + token,
                new StringContent(JsonSerializer.Serialize(new
                {
                    url=cred.Url,
                    tileset= parseResult.GetValueForOption(TileSet), //"kjeldager.bukom2022",
                    name= parseResult.GetValueForOption(NameOption) // "DronePoul.com - Bukum 2022 - Besesaka",



                }), Encoding.UTF8,"application/json"));



            // console.WriteLine("Hello world:\n"+ await cred.Content.ReadAsStringAsync());
        }
    }
}