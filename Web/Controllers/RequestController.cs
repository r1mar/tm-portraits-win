﻿using AutoMapper;
using AutoMapper.QueryableExtensions;
using TuRM.Portrait.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Helpers;
using System.Web.Mvc;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Net;

namespace TuRM.Portrait.Controllers
{
    [RequireHttps]
    public class RequestController : Controller
    {
        // GET: Work
        [Authorize]
        public ActionResult Index()
        {
            ViewModels.Request.Index viewModel = new ViewModels.Request.Index();

            using (ApplicationDbContext db = new ApplicationDbContext())
            {
                MapperConfiguration config = new MapperConfiguration(cfg =>
                {
                    cfg.CreateMap<RequestHead, ViewModels.Request.Head>();
                });
                viewModel.Header = db.Database.SqlQuery<RequestHead>("select * from " + nameof(RequestHead) + "s "
                                                                        + "where Id not in (select " + nameof(RequestCancellation.RequestHeadId) + " from RequestCancellations)")
                                                                        .ToList()
                    .AsQueryable()
                    .ProjectTo<ViewModels.Request.Head>(config)
                    .ToList();
            }

            return View(viewModel);
        }

        [HttpGet]
        [Authorize]
        public ActionResult Detail(int? Id, int? RequestItemId)
        {
            ViewModels.Request.Detail viewModel = new ViewModels.Request.Detail();
            RequestHead head;

            using (ApplicationDbContext db = new ApplicationDbContext())
            {
                MapperConfiguration config = new MapperConfiguration(cfg =>
                {
                    cfg.CreateMap<RequestHead, ViewModels.Request.Head>();
                    cfg.CreateMap<RequestItem, ViewModels.Request.Item>();
                    cfg.CreateMap<RequestImage, ViewModels.Request.Image>()
                        .ForMember(dest => dest.Data, opt => opt.MapFrom(src => new WebImage(src.Data))); ;
                });
                IMapper mapper = config.CreateMapper();

                if (RequestItemId.HasValue)
                {
                    Id = db.RequestItems.Where(w => w.Id == RequestItemId).Select(s => s.RequestHeadId).First();
                }

                head = db.RequestHeads.Where(w => w.Id == Id).First();
                viewModel.Head = mapper.Map<ViewModels.Request.Head>(head);

                db.Entry(head).Collection(c => c.RequestItems).Load();
                viewModel.Items = head.RequestItems.ToList().AsQueryable().ProjectTo<ViewModels.Request.Item>(config).ToList();

                db.Entry(head).Collection(c => c.RequestImages).Load();
                viewModel.Images = mapper.Map<List<ViewModels.Request.Image>>(head.RequestImages);
              
            }

            return View(viewModel);
        }

        [Authorize]
        public ActionResult ShowImage(int Id)
        {
            RequestImage image;

            using (ApplicationDbContext db = new ApplicationDbContext())
            {
                image = db.RequestImages.Where(w => w.Id == Id).First();
            }

            return File(image.Data, new WebImage(image.Data).ImageFormat);
        }

        [HttpGet]
        public ActionResult Create(int? productId)
        {
            ViewModels.Request.Create viewModel;
            IQueryable<Product> collection; 

            using (ApplicationDbContext db = new ApplicationDbContext())
            {
                MapperConfiguration config = new MapperConfiguration(cfg =>
                {
                    cfg.CreateMap<Product, ViewModels.Product.Product>()
                        .ForMember(m => m.Image, opt => opt.MapFrom(src => src.Image == null ? null : new WebImage(src.Image)));
                });
                IMapper mapper = config.CreateMapper();

                viewModel = new ViewModels.Request.Create();

                collection = db.Products.Where(w => w.ProductCategoryId == 1);
                if (productId != null)
                {
                    collection = collection.Where(w => w.Id == productId);
                }
                viewModel.Products = collection.ToList().AsQueryable().ProjectTo<ViewModels.Product.Product>(config).ToList().AsQueryable();
                viewModel.ProductId = viewModel.Products.First().Id;

                collection = db.Products.Where(w => w.ProductCategoryId == 2);
                viewModel.Sizes = collection.ToList().AsQueryable().ProjectTo<ViewModels.Product.Product>(config).ToList().AsQueryable();
                viewModel.SizeId = viewModel.Sizes.First().Id;

                viewModel.SubjectProduct = mapper.Map<ViewModels.Product.Product>(db.Products.Where(w => w.ProductCategoryId == 3).First());

                viewModel.TotalAmount = viewModel.Products.First().Price
                    + viewModel.Sizes.First().Price
                    + (viewModel.SubjectProduct.Price * (viewModel.CountSubjects - 1));
            }

            viewModel.Files = HttpContext.Session["Files"] as SortedList<string, WebImage> ?? new SortedList<string, WebImage>();

            return View(viewModel);
        }

        private void loadAdjustments(Product product, ApplicationDbContext db, RequestHead head)
        {
            RequestItem item;

            db.Entry(product).Collection(p => p.PriceAdjustments).Load();
            for (int i = 0; i < product.PriceAdjustments?.Count; i++)
            {
                item = new RequestItem();
                item.RequestHeadId = head.Id;
                item.Amount = 1;
                item.Name = product.PriceAdjustments.ElementAt(i).Name;
                item.Price = product.PriceAdjustments.ElementAt(i).Adjustment;

                db.RequestItems.Add(item);
            }
        }

        public async Task<ActionResult> Create(ViewModels.Request.Create viewModel)
        {
            if (ModelState.IsValid)
            {
                using (ApplicationDbContext db = new ApplicationDbContext())
                {
                    MapperConfiguration config = new MapperConfiguration(cfg =>
                    {
                        cfg.CreateMap<ViewModels.Request.Create, RequestHead>()
                            .ForMember(m=>m.CreatedOn, opt=> opt.UseValue(DateTime.Now));
                    });
                    IMapper mapper = config.CreateMapper();
                    RequestHead head = mapper.Map<RequestHead>(viewModel);
                    RequestItem item;
                    RequestImage image;
                    Product product;

                    db.RequestHeads.Add(head);

                    db.SaveChanges();

                    db.Entry(head).Reload();

                    product = db.Products.Where(w => w.Id == viewModel.ProductId).First();
                    item = new RequestItem();
                    item.RequestHeadId = head.Id;
                    item.Name = product.Name;
                    item.Amount = 1;
                    item.Price = product.Price;
                    db.RequestItems.Add(item);

                    loadAdjustments(product, db, head);

                    product = db.Products.Where(w => w.Id == viewModel.SizeId).First();
                    item = new RequestItem();
                    item.RequestHeadId = head.Id;
                    item.Name = product.Name;
                    item.Amount = 1;
                    item.Price = product.Price;
                    db.RequestItems.Add(item);

                    loadAdjustments(product, db, head);

                    product = db.Products.Where(w => w.ProductCategoryId == 3).First();
                    item = new RequestItem();
                    item.RequestHeadId = head.Id;
                    item.Name = product.Name;
                    item.Amount = viewModel.CountSubjects;
                    item.Price = product.Price;
                    db.RequestItems.Add(item);

                    loadAdjustments(product, db, head);

                    viewModel.Files = HttpContext.Session["Files"] as SortedList<string, WebImage> ?? new SortedList<string, WebImage>();
                    for (int i = 0; i < viewModel.Files.Values.Count; i++)
                    {
                        image = new RequestImage();
                        image.RequestHeadId = head.Id;
                        image.Data = viewModel.Files.Values[i].GetBytes();
                        db.RequestImages.Add(image);
                    }

                    await db.SaveChangesAsync();

                }
                HttpContext.Session["Files"] = null;

                RedirectToAction(nameof(Index));

                sendNotificationEmail();
            }

            return View("CreateConfirm");
        }

        private void sendNotificationEmail()//string firstName, string secondName
        {
            try
            {
                Object token = new object();
                using (SmtpClient client = new SmtpClient("smtp.strato.de", 587))
                {
                    MailMessage message = new MailMessage();

                    message.From = new MailAddress("webmaster@tm-portraits.de");
                    message.To.Add("kontakt@tm-portraits.de");
                    message.Subject = "Neue Bestellung eingetroffen";
                    message.Body = $"{Server.MachineName}: Es ist eine neue Bestellung eingetroffen";//von {firstName} {secondName} 
                    message.IsBodyHtml = false;

                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential("webmaster@tm-portraits.de", "architekTur25");
                    client.DeliveryMethod = SmtpDeliveryMethod.Network;
                    client.EnableSsl = true;
                    client.SendCompleted += Client_SendCompleted;

                    client.Send(message);
                }
            }
            catch (Exception ex)
            {
                
            }
        }
        
        private void Client_SendCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            return;
        }

        [HttpPost]
        public void AddFile()
        {
            ViewModels.Request.Create viewModel = new ViewModels.Request.Create();
            HttpPostedFileBase file;

            viewModel.Files = HttpContext.Session["Files"] as SortedList<string, WebImage> ?? new SortedList<string, WebImage>();

            for (int i = 0; i < Request.Files.Count; i++)
            {
                file = Request.Files[i];
                if (viewModel.Files.ContainsKey(Request.Files.GetKey(i)))
                {
                    viewModel.Files[Request.Files.GetKey(i)] = new WebImage(file.InputStream);
                }
                else
                {
                    viewModel.Files.Add(Request.Files.GetKey(i), new WebImage(file.InputStream));
                }
            }

            HttpContext.Session["Files"] = viewModel.Files;

            //using (ApplicationDbContext db = new ApplicationDbContext())
            //{
            //    viewModel = new ViewModels.Request.Create(Product.Get(), Size.Get());
            //}

            //return View("FileList", viewModel.Files);

        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public void RemoveFile(string id)
        {
            ViewModels.Request.Create viewModel = new ViewModels.Request.Create();

            viewModel.Files = HttpContext.Session["Files"] as SortedList<string, WebImage> ?? new SortedList<string, WebImage>();

            viewModel.Files.Remove(id);

            HttpContext.Session["Files"] = viewModel.Files;

            //return View("Create", viewModel);
        }

    }
}