using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using System.Net;

using IPA.Cores.Basic;
using IPA.Cores.Helper.Basic;
using static IPA.Cores.Globals.Basic;

using IPA.Cores.Web;
using IPA.Cores.Helper.Web;
using static IPA.Cores.Globals.Web;

using IPA.Cores.Codes;
using IPA.Cores.Helper.Codes;
using static IPA.Cores.Globals.Codes;

using IPA.DN.FileCenter;
using IPA.DN.FileCenter.Models;

namespace IPA.DN.FileCenter
{
    [Route(FileCenterConsts.FileBrowserHttpDir + "/{*path}")]
    public class FileCenterBrowserController : Controller
    {
        public async Task<IActionResult> Index([FromServices] Server server)
        {
            string fullpath = Request._GetRequestPathAndQueryString();

            if (fullpath._TryTrimStartWith(out string path, StringComparison.OrdinalIgnoreCase, FileCenterConsts.FileBrowserHttpDir) == false)
            {
                return new ContentResult { Content = "Invalid URL", ContentType = "text/plain", StatusCode = 404 };
            }
            else
            {
                HttpResult result = await server.ProcessFileBrowserRequestAsync(
                    HttpContext.Connection.RemoteIpAddress._UnmapIPv4(),
                    HttpContext.Connection.RemotePort,
                    Request._GetRequestPathAndQueryString(),
                    Request, Response,
                    this._GetRequestCancellationToken());

                return result.GetHttpActionResult();
            }
        }
    }
}
