using System;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using DemoClassLibrary;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DemoFunctionsApp
{
    public class Function1
    {

        private IConfiguration Configuration;
        public Function1(IConfiguration _configuration)
        {
            Configuration = _configuration;
        }

        [FunctionName("Function1")]
        public void Run([QueueTrigger("thumbnailrequest", Connection = "")] BlobInformation blobInfo,
            [Blob("images/{BlobName}", FileAccess.Read)] Stream input,
            [Blob("images/{BlobNameWithoutExtension}_thumbnail.jpg")] CloudBlockBlob outputBlob,
            ILogger log)
        {

            using (Stream output = outputBlob.OpenWrite())
            {
                ConvertImageToThumbnailJPG(input, output);
                outputBlob.Properties.ContentType = "image/jpeg";
            }
            //SqlConnection connection = new SqlConnection
            //{
            //    ConnectionString = Environment.GetEnvironmentVariable("DemoDbContext")
            //};
            //string sql = $"Update Employee Set ThumbnailUrl = '{outputBlob.Uri.ToString()}' where Id ={ blobInfo.EmpId}";
            //SqlCommand cmd = new SqlCommand(sql, connection);
            //connection.Open();
            //cmd.ExecuteNonQuery();
            //connection.Close();
            //log.LogInformation("Connection Closed");


            //To run function locally
            //var options = new DbContextOptionsBuilder<DemoDbContext>();
            //options.UseSqlServer(Environment.GetEnvironmentVariable("DemoDbContext"));

            //To run function on server
            var options = new DbContextOptionsBuilder<DemoDbContext>();
            options.UseSqlServer(this.Configuration.GetConnectionString("DemoDbContext"));


            var db = new DemoDbContext(options.Options);

            var id = blobInfo.EmpId;
            Employee emp = db.Employee.Find(id);
            if (emp == null)
            {
                throw new Exception(String.Format("EmpId: {0} not found, can't create thumbnail", id.ToString()));
            }
            emp.ThumbnailUrl = outputBlob.Uri.ToString();
            db.SaveChanges();  

        }
        public void ConvertImageToThumbnailJPG(Stream input, Stream output)
        {
            int thumbnailsize = 80;
            int width;
            int height;
            var originalImage = new Bitmap(input);
            if (originalImage.Width > originalImage.Height)
            {
                width = thumbnailsize;
                height = thumbnailsize * originalImage.Height / originalImage.Width;
            }
            else
            {
                height = thumbnailsize;
                width = thumbnailsize * originalImage.Width / originalImage.Height;
            }
            Bitmap thumbnailImage = null;
            try
            {
                thumbnailImage = new Bitmap(width, height);
                using (Graphics graphics = Graphics.FromImage(thumbnailImage))
                {
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.DrawImage(originalImage, 0, 0, width, height);
                }
                thumbnailImage.Save(output, System.Drawing.Imaging.ImageFormat.Jpeg);

            }
            finally
            {
                if (thumbnailImage != null)
                {
                    thumbnailImage.Dispose();
                }
            }
        }
    }
}
