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
    public class InboxController : Controller
    {
        readonly Server server;

        private readonly ILogger<InboxController> _logger;

        public InboxController(ILogger<InboxController> logger, Server server)
        {
            _logger = logger;

            this.server = server;
        }

        public IActionResult UploadForm(PageContext page)
        {
            string baseUrl = Request.GetDisplayUrl()._ParseUrl()._CombineUrl("/").ToString();

            string curlCmdLine =
                $"$ curl {baseUrl}Uploader/Upload -f -F -F \"json=true\" -F \"getfile=false\" -F \"getdir=false\" -F \"days=0\" -F \"auth=false\" -F \"log=true\" -F \"once=false\" -F \"urlhint=testfile\" -F \"zip=false\" -F \"file=@送信ファイル１\" -F \"file=@送信ファイル２\"";

            ViewBag.curl = curlCmdLine;

            UploadFormCookies? cookie = new UploadFormCookies(); // dummy

            return View(cookie);
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

            UploadFormCookies? cookie = this._EasyLoadCookie<UploadFormCookies>("InboxCreateForm");

            if (cookie == null) cookie = new UploadFormCookies();

            string baseUrl = Request.GetDisplayUrl()._ParseUrl()._CombineUrl("/").ToString();

            string curlCmdLine =
                $"$ curl {baseUrl}Uploader/Upload -f -F \"pin={currentPin}\" -F \"json=true\" -F \"getfile=false\" -F \"getdir=false\" -F \"days=0\" -F \"auth=false\" -F \"log=true\" -F \"once=false\" -F \"urlhint=testfile\" -F \"zip=false\" -F \"file=@送信ファイル１\" -F \"file=@送信ファイル２\"";

            ViewBag.curl = curlCmdLine;

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
            return Redirect(FileCenterConsts.FileBrowserUploadInboxHttpDir + "/");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsync(UploadFormRequest form, CancellationToken cancel)
        {
            using UploadFileList fl = new UploadFileList();

            UploadOption opt = new UploadOption
            {
                IsInboxCreateMode = true,
                Auth = form.Auth,
                Days = form.Days,
                Destination = "",
                LogAccess = form.Log,
                Once = form.Once,
                UrlHint = form.UrlHint,
                VeryShort = form.VeryShort,
                Zip = false,
                Email = form.Email,
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
                Email = form.Email._NonNullTrim(),
            };

            this._EasySaveCookie("InboxCreateForm", cookie);

            try
            {
                var result = await server.UploadAsync(DateTimeOffset.Now,
                    Request.HttpContext.Connection.RemoteIpAddress._UnmapIPv4()!.ToString(),
                    Request.GetDisplayUrl(),
                    fl,
                    opt,
                    cancel);

                return View("ResultInboxCreate", result);
            }
            catch
            {
                throw;
            }
        }

        [RequestSizeLimit(FileCenterConsts.UploadSizeHardLimit)]
        [RequestFormLimits(MultipartBodyLengthLimit = FileCenterConsts.UploadSizeHardLimit)]
        [DisableRequestSizeLimit]
        [HttpPost]
        public async Task<IActionResult> UploadAsync(UploadFormRequest form,
            List<IFormFile> file,
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
            string? pin,
            bool json,
            bool getfile,
            bool getdir,
            CancellationToken cancel)
        {
            using UploadFileList fl = new UploadFileList();

            fl.AddFormFileList(file, "");
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
                VeryShort = form.VeryShort,
                Zip = form.Zip,
                PIN = this._EasyLoadCookie<string>("pin")._NonNullTrim(),
            };

            if (pin._IsFilled()) opt.PIN = pin;

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

            try
            {
                var result = await server.UploadAsync(DateTimeOffset.Now,
                    Request.HttpContext.Connection.RemoteIpAddress._UnmapIPv4()!.ToString(),
                    Request.GetDisplayUrl(),
                    fl,
                    opt,
                    cancel);

                if (getfile)
                {
                    return new HttpStringResult(result.GeneratedUrlFirstFileAuthCredentialDirect + "\r\n").GetHttpActionResult();
                }

                if (getdir)
                {
                    return new HttpStringResult(result.GeneratedUrlDirAuthCredentialDirect + "\r\n").GetHttpActionResult();
                }

                if (json)
                {
                    return result._AspNetJsonResult();
                }

                return View("Result", result);
            }
            catch (Exception ex)
            {
                if (json || getfile || getdir)
                {
                    return new HttpStringResult("Error: " + ex.Message + "\r\n", statusCode: Consts.HttpStatusCodes.InternalServerError).GetHttpActionResult();
                }

                throw;
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier, ErrorInfo = this._GetLastError() });
        }
    }
}
