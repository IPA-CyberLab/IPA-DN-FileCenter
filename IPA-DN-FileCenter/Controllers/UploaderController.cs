using System;
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
using IPA.Cores.Basic;
using IPA.Cores.Helper.Web;
using Org.BouncyCastle.Crypto;

namespace IPA.DN.FileCenter.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class UploaderController : Controller
    {
        readonly Server server;

        private readonly ILogger<UploaderController> _logger;

        public UploaderController(ILogger<UploaderController> logger, Server server)
        {
            _logger = logger;

            this.server = server;
        }

        public IActionResult Index(PageContext page)
        {
            // PIN コードチェック
            string? cookiePin = this._EasyLoadCookie<string>("pin")._NonNullTrim();
            string? currentPin = server.DbSnapshot.PIN;
            if (currentPin._IsFilled() && currentPin._IsSame(cookiePin) == false)
            {
                // PIN コード不正。入力ページに飛ばす
                return View("PIN");
            }

            UploadFormCookies? cookie = this._EasyLoadCookie<UploadFormCookies>("uploadForm");

            if (cookie == null) cookie = new UploadFormCookies();

            return View(cookie);
        }

        public IActionResult PIN(PageContext page, string? pin)
        {
            pin = pin._NonNullTrim();
            // 入力された PIN コードのチェック
            string? currentPin = server.DbSnapshot.PIN;
            if (currentPin._IsFilled() && currentPin._IsSame(pin) == false)
            {
                // PIN コード不正。入力ページに飛ばす
                ViewBag.Incorrect = true;
                return View("PIN");
            }

            this._EasySaveCookie("pin", pin);

            // トップページに移動
            return Redirect("/");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [RequestSizeLimit(FileCenterConsts.UploadSizeHardLimit)]
        [RequestFormLimits(MultipartBodyLengthLimit = FileCenterConsts.UploadSizeHardLimit)]
        [DisableRequestSizeLimit]
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
                Destination = form.Recipient,
                LogAccess = form.Log,
                Once = form.Once,
                UrlHint = form.UrlHint,
                Zip = form.Zip,
                PIN = this._EasyLoadCookie<string>("pin")._NonNullTrim(),
            };

            opt.Normalize();

            UploadFormCookies cookie = new UploadFormCookies
            {
                Auth = form.Auth,
                Log = form.Log,
                //Once = form.Once,
                //Days = form.Days,
                Zip = form.Zip,
            };

            this._EasySaveCookie("uploadForm", cookie);

            var result = await server.UploadAsync(DateTimeOffset.Now,
                Request.HttpContext.Connection.RemoteIpAddress._UnmapIPv4().ToString(),
                Request.GetDisplayUrl(),
                fl,
                opt,
                cancel);

            //return result._ObjectToJson()._AspNetTextActionResult();

            return View("Result", result);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier, ErrorInfo = this._GetLastError() });
        }
    }
}
