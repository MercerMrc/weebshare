using System;
using baka.Models;
using Amazon.S3;
using Amazon.S3.Model;
using System.Threading.Tasks;
using System.IO;
using Amazon.S3.Transfer;
using Jose;
using baka.Models.Entity;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using System.Text;

namespace baka
{
    public static class Globals
    {
        public const string ConfigFileName = "baka_config.json";

        public const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

        public static Random Random { get; set; }

        public static ConfigModel Config { get; set; }

        public static AmazonS3Config S3Config { get; set; }

        public static AmazonS3Client S3Client { get; set; }

        public static TransferUtility S3Utility { get; set; }

        public static void Initliaze()
        {
            Config = ConfigModel.GetConfig(ConfigFileName);

            S3Config = new AmazonS3Config();
            if (Config.S3ServiceURL != null)
                S3Config.ServiceURL = Config.S3ServiceURL;

            S3Client = new AmazonS3Client(Config.S3AccessKey, Config.S3SecretKey, S3Config);

            S3Utility = new TransferUtility(S3Client);

            Random = new Random();

            InitliazeDb().GetAwaiter().GetResult();
        }

        private static async Task InitliazeDb()
        {
            using (var context = new BakaContext())
            {
                await context.Database.EnsureCreatedAsync();

                var root_user = await context.Users.FirstOrDefaultAsync(x => x.Token == Config.RootToken);

                if (root_user != null)
                {
                    Console.WriteLine("Root user already exists!\n" + JsonConvert.SerializeObject(root_user));
                    return;
                }

                root_user = new BakaUser()
                {
                    Name = "ROOT_USER",
                    Username = "ROOT_USER",
                    InitialIp = "ROOT_USER",
                    UploadLimitMB = 500,
                    Deleted = false,
                    Disabled = false,
                    Timestamp = DateTime.Now,
                    Email = "ROOT_USER_DO_NOT_USE_FOR_UPLOADING_OBJECTS",
                    Token = Config.RootToken,
                    AccountType = "su_full"
                };

                await context.Users.AddAsync(root_user);
                await context.SaveChangesAsync();

                Console.WriteLine("Created root user!\n" + JsonConvert.SerializeObject(root_user));
            }
        }

        public static async Task<bool> UploadFile(Stream stream, string FileId, string ContentType)
        {
            try
            {
                TransferUtilityUploadRequest request = new TransferUtilityUploadRequest();
                request.BucketName = Config.S3BucketName;
                request.Key = FileId;
                request.InputStream = stream;
                request.ContentType = ContentType;

                if (Config.SetS3Public)
                    request.CannedACL = S3CannedACL.PublicRead;

                await S3Utility.UploadAsync(request);

                return true;
            }
            catch (Exception e)
            {
                if (Globals.Config.IsDebug)
                    Console.WriteLine("[S3] " + e.ToString());
                return false;
            }
        }

        public static async Task<Stream> GetFile(string FileId)
        {
            try
            {
                TransferUtilityOpenStreamRequest request = new TransferUtilityOpenStreamRequest();
                request.BucketName = Config.S3BucketName;
                request.Key = FileId;

                return await S3Utility.OpenStreamAsync(request);
            }
            catch (Exception e)
            {
                if (Globals.Config.IsDebug)
                    Console.WriteLine("[S3] " + e.ToString());

                return null;
            }
        }

        public static async Task<bool> DeleteFile(string FileId)
        {
            try
            {
                DeleteObjectRequest request = new DeleteObjectRequest();
                request.BucketName = Config.S3BucketName;
                request.Key = FileId;

                await S3Client.DeleteObjectAsync(request);

                return true;
            }
            catch (Exception e)
            {
                if (Globals.Config.IsDebug)
                    Console.WriteLine("[S3] " + e.ToString());

                return false;
            }
        }

        public static string GenerateToken(BakaUser user)
        {
            var payload = new
            {
                ts = DateTime.Now.ToFileTimeUtc().ToString()
            };

            byte[] key = Encoding.UTF8.GetBytes(Config.JWTKey);

            string token = JWT.Encode(payload, key, JwsAlgorithm.HS256);

            return token;
        }

        public static string GenerateFileId()
        {
            return new string(Enumerable.Repeat(chars, Config.IdLength)
              .Select(s => s[Random.Next(s.Length)]).ToArray());
        }

        public static string GenerateBackendId()
        {
            return new string(Enumerable.Repeat(chars, Config.IdLength * 2)
              .Select(s => s[Random.Next(s.Length)]).ToArray());
        }

        public static double ConvertBytesToMegabytes(long bytes)
        {
            return (bytes / 1024d) / 1024d;
        }

        public static double ConvertMegabytesToBytes(long mb)
        {
            return (mb * 1024d) * 1024d;
        }

        public static double ConvertKilobytesToMegabytes(long kilobytes)
        {
            return kilobytes / 1024f;
        }
    }
}