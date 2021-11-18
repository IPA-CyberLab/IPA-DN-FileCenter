using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using IPA.Cores.Basic;
using IPA.Cores.Helper.Basic;
using static IPA.Cores.Globals.Basic;

using IPA.Cores.Web;
using IPA.Cores.Helper.Web;
using static IPA.Cores.Globals.Web;

using IPA.Cores.Codes;
using IPA.Cores.Helper.Codes;
using static IPA.Cores.Globals.Codes;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;

using IPA.DN.FileCenter;
using System.Threading;

namespace DaemonCenter.Controllers;

[AutoValidateAntiforgeryToken]
public class DeleteController : Controller
{
    readonly Server Server;

    public DeleteController(Server server)
    {
        this.Server = server;
    }

    [HttpGet]
    public IActionResult Index()
    {
        DeleteForm delete = new DeleteForm();

        return View("Index", delete);
    }

    [HttpPost]
    public async Task<IActionResult> IndexAsync([FromForm] DeleteForm delete, CancellationToken cancel)
    {
        bool authed = (User.Identity?.IsAuthenticated ?? false);

        if (delete.Force && authed == false && delete.Code._IsEmpty())
        {
            throw new CoresException("強制削除機能がチェックされていますが、システム管理者モードでログインしていません。ページ上部の「システム設定」ボタンをクリックして、システム管理者モードでログインしてから続行してください。");
        }

        await this.Server.DeleteAsync(DtOffsetNow, Request.HttpContext.Connection.RemoteIpAddress._UnmapIPv4()!.ToString(), delete.Url._NonNullTrim(), delete.Code._NonNullTrim(), delete.Force, cancel);

        return View("Result", delete);
    }
}
