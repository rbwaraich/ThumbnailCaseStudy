#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using DemoClassLibrary;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Azure.Storage.Queue;
using Newtonsoft.Json;


namespace DemoWebApp.Views
{
    public class EmployeesController : Controller
    {
        private readonly DemoDbContext _context;
        private IConfiguration Configuration;

        public EmployeesController(DemoDbContext context, IConfiguration _configuration)
        {
            _context = context;
            Configuration = _configuration;
        }

        // GET: Employees
        public async Task<IActionResult> Index()
        {
            return View(await _context.Employee.ToListAsync());
        }

        // GET: Employees/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var employee = await _context.Employee
                .FirstOrDefaultAsync(m => m.Id == id);
            if (employee == null)
            {
                return NotFound();
            }

            return View(employee);
        }

        // GET: Employees/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Employees/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,EmpName,Salary,ImageUrl,ThumbnailUrl")] Employee employee, List<IFormFile> imageFile)
        {
            if (ModelState.IsValid)
            {
                employee.ImageUrl = UploadImage(imageFile);
                _context.Add(employee);
                await _context.SaveChangesAsync();
                PostMessageToQueue(employee.Id, employee.ImageUrl);
                return RedirectToAction(nameof(Index));
            }
            return View(employee);
        }

        // GET: Employees/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var employee = await _context.Employee.FindAsync(id);
            if (employee == null)
            {
                return NotFound();
            }
            return View(employee);
        }

        // POST: Employees/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,EmpName,Salary,ImageUrl,ThumbnailUrl")] Employee employee)
        {
            if (id != employee.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(employee);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EmployeeExists(employee.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(employee);
        }

        // GET: Employees/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var employee = await _context.Employee
                .FirstOrDefaultAsync(m => m.Id == id);
            if (employee == null)
            {
                return NotFound();
            }

            return View(employee);
        }

        // POST: Employees/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var employee = await _context.Employee.FindAsync(id);
            _context.Employee.Remove(employee);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool EmployeeExists(int id)
        {
            return _context.Employee.Any(e => e.Id == id);
        }

        public string UploadImage(List<IFormFile> imageFile)
        {
            //Write code here to Storage Image in Azure Blob Storage and Get the URL of image
            var storageAccount =
           CloudStorageAccount.Parse(this.Configuration.GetConnectionString("AzureWebJobsStorage"));
            //To Upload the Image into the Blob Container           

            long size = imageFile.Sum(f => f.Length);
            foreach (var formFile in imageFile)
            {
                if (formFile.Length > 0)
                {
                    //var filePath = Path.GetTempFileName();
                    //var fileStream = new FileStream(filePath, FileMode.Create);
                    //formFile.CopyTo(fileStream);
                    var fileStream = formFile.OpenReadStream();
                    string blobName = Guid.NewGuid().ToString() + Path.GetExtension(formFile.FileName);
                    var blobClient = storageAccount.CreateCloudBlobClient();
                    CloudBlobContainer imagesBlobContainer = blobClient.GetContainerReference("images");
                    imagesBlobContainer.CreateIfNotExists();
                    CloudBlockBlob imageBlob = imagesBlobContainer.GetBlockBlobReference(blobName);  
                    imageBlob.UploadFromStream(fileStream);
                    fileStream.Close();
                    return imageBlob.Uri.ToString();
                }
            }

            return string.Empty;
            
        }

        private void PostMessageToQueue(int empId, string imageUrl)
        {
            var storageAccount =
           CloudStorageAccount.Parse(this.Configuration.GetConnectionString("AzureWebJobsStorage"));
            //To create the Queue with BlobInformation
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            CloudQueue thumbnailRequestQueue = queueClient.GetQueueReference("thumbnailrequest");
            thumbnailRequestQueue.CreateIfNotExists();
            BlobInformation blobInfo = new BlobInformation() { EmpId = empId, BlobUri = new Uri(imageUrl) };
            var queueMessage = new CloudQueueMessage(JsonConvert.SerializeObject(blobInfo));
            thumbnailRequestQueue.AddMessage(queueMessage);
        }
    }
}
