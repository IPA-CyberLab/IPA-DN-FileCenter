﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using IPA.DN.FileCenter.Models;
using IPA.Cores.Helper.Basic;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using System.Threading;

namespace IPA.DN.FileCenter.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class HomeController : Controller
    {
        readonly Server server;

        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger, Server server)
        {
            _logger = logger;

            this.server = server;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        public async Task<IActionResult> UploadAsync(UploadFormRequest form,
            List<IFormFile> file_1,
            List<IFormFile> file_2,
            List<IFormFile> file_3,
            List<IFormFile> file_4,
            List<IFormFile> file_5,
            List<IFormFile> file_6,
            List<IFormFile> file_7,
            List<IFormFile> file_8,
            List<IFormFile> file_9,
            List<IFormFile> file_10,
            CancellationToken cancel)
        {
            using UploadFileList fl = new UploadFileList();

            fl.AddFormFileList(file_1, form.dirname_1);
            fl.AddFormFileList(file_2, form.dirname_2);
            fl.AddFormFileList(file_3, form.dirname_3);
            fl.AddFormFileList(file_4, form.dirname_4);
            fl.AddFormFileList(file_5, form.dirname_5);
            fl.AddFormFileList(file_6, form.dirname_6);
            fl.AddFormFileList(file_7, form.dirname_7);
            fl.AddFormFileList(file_8, form.dirname_8);
            fl.AddFormFileList(file_9, form.dirname_9);
            fl.AddFormFileList(file_10, form.dirname_10);

            UploadOption opt = new UploadOption
            {
                Auth = form.Auth,
                Days = form.Days,
                Destination = form.Destination,
                LogAccess = form.Log,
                Once = form.Once,
                UrlHint = form.UrlHint,
                Zip = form.Zip,
            };

            opt.Normalize();

            await server.UploadAsync(DateTimeOffset.Now,
                Request.HttpContext.Connection.RemoteIpAddress._UnmapIPv4().ToString(),
                Request.GetDisplayUrl(),
                fl,
                opt,
                cancel);

            return View("Privacy");
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
