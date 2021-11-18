using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;

using IPA.Cores.Basic;
using IPA.Cores.Helper.Basic;
using static IPA.Cores.Globals.Basic;

using IPA.Cores.Web;
using IPA.Cores.Helper.Web;
using static IPA.Cores.Globals.Web;

using IPA.Cores.Codes;
using IPA.Cores.Helper.Codes;
using static IPA.Cores.Globals.Codes;

using Microsoft.AspNetCore.Mvc.Razor;

namespace IPA.DN.FileCenter;

// IPA.DN.FileCenter 用のページコンテキスト
public class PageContext : AspPageContext
{
    public DateTimeOffset Now = DateTimeOffset.Now;

    // サイト名
    public PageContext()
    {
        this.SiteName = "IPA FileCenter セキュアファイルアップロードシステム";
    }
}

