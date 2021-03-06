/* http://www.zkea.net/ Copyright 2016 ZKEASOFT http://www.zkea.net/licenses */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Easy.Constant;
using Easy.Data;
using Easy.Extend;
using Easy.Web;
using Easy.Web.Attribute;
using Easy.Web.Authorize;
using Easy.Web.CMS;
using Easy.Web.CMS.Widget;
using Easy.Web.CMS.WidgetTemplate;
using Easy.Web.ValueProvider;
using Newtonsoft.Json;
using System.IO;
using Easy.Web.CMS.PackageManger;
using Microsoft.Practices.ServiceLocation;
using Easy.Modules.DataDictionary;

namespace Easy.CMS.Common.Controllers
{
    [AdminTheme, DefaultAuthorize(PermissionKeys.ManagePage)]
    public class WidgetController : Controller
    {
        private readonly IWidgetService _widgetService;
        private readonly IWidgetTemplateService _widgetTemplateService;
        private readonly ICookie _cookie;
        private readonly IPackageInstallerProvider _packageInstallerProvider;

        public WidgetController(IWidgetService widgetService, IWidgetTemplateService widgetTemplateService,
            ICookie cookie, IPackageInstallerProvider packageInstallerProvider)
        {
            _widgetService = widgetService;
            _widgetTemplateService = widgetTemplateService;
            _cookie = cookie;
            _packageInstallerProvider = packageInstallerProvider;
        }

        [ViewDataZones]
        public ActionResult Create(QueryContext context)
        {
            var template = _widgetTemplateService.Get(context.WidgetTemplateID);
            var widget = template.CreateWidgetInstance();
            widget.PageID = context.PageID;
            widget.LayoutID = context.LayoutID;
            widget.ZoneID = context.ZoneID;
            widget.FormView = template.FormView;
            if (widget.PageID.IsNotNullAndWhiteSpace())
            {
                widget.Position = _widgetService.GetAllByPageId(context.PageID).Count(m => m.ZoneID == context.ZoneID) + 1;
            }
            else
            {
                widget.Position = _widgetService.GetByLayoutId(context.LayoutID).Count(m => m.ZoneID == context.ZoneID) + 1;
            }
            ViewBag.WidgetTemplateName = template.Title;
            ViewBag.ReturnUrl = context.ReturnUrl;
            if (template.FormView.IsNotNullAndWhiteSpace())
            {
                return View(template.FormView, widget);
            }
            return View(widget);
        }
        [HttpPost, ViewDataZones]
        [ValidateInput(false)]
        public ActionResult Create(WidgetBase widget, string ReturnUrl)
        {
            if (!ModelState.IsValid)
            {
                return View(widget);
            }
            widget.CreateServiceInstance().AddWidget(widget);
            if (widget.ActionType == ActionType.Continue)
            {
                return RedirectToAction("Edit", new { widget.ID, ReturnUrl });
            }
            if (!ReturnUrl.IsNullOrEmpty())
            {
                return Redirect(ReturnUrl);
            }
            if (!widget.PageID.IsNullOrEmpty())
            {
                return RedirectToAction("Design", "Page", new { module = "admin", ID = widget.PageID });
            }
            return RedirectToAction("LayoutWidget", "Layout", new { module = "admin" });
        }
        [ViewDataZones]
        public ActionResult Edit(string ID, string ReturnUrl)
        {
            var widgetBase = _widgetService.Get(ID);
            var widget = widgetBase.CreateServiceInstance().GetWidget(widgetBase);
            ViewBag.ReturnUrl = ReturnUrl;

            var template = _widgetTemplateService.Get(
                m =>
                    m.PartialView == widget.PartialView && m.AssemblyName == widget.AssemblyName &&
                    m.ServiceTypeName == widget.ServiceTypeName && m.ViewModelTypeName == widget.ViewModelTypeName).FirstOrDefault();
            if (template != null)
            {
                ViewBag.WidgetTemplateName = template.Title;
            }
            if (widget.FormView.IsNotNullAndWhiteSpace())
            {
                return View(widget.FormView, widget);
            }
            return View(widget);
        }

        [HttpPost, ViewDataZones]
        [ValidateInput(false)]
        public ActionResult Edit(WidgetBase widget, string ReturnUrl)
        {
            if (!ModelState.IsValid)
            {
                return View(widget);
            }
            widget.CreateServiceInstance().UpdateWidget(widget);
            if (!ReturnUrl.IsNullOrEmpty())
            {
                return Redirect(ReturnUrl);
            }
            if (!widget.PageID.IsNullOrEmpty())
            {
                return RedirectToAction("Design", "Page", new { module = "admin", ID = widget.PageID });
            }
            return RedirectToAction("LayoutWidget", "Layout", new { module = "admin" });
        }

        [HttpPost]
        public JsonResult SaveWidgetZone(List<WidgetBase> widgets)
        {
            foreach (var widget in widgets)
            {
                _widgetService.Update(widget, new DataFilter(new List<string> { "ZoneID", "Position" }).Where("ID", OperatorType.Equal, widget.ID));
            }
            return Json(true);
        }
        [HttpPost]
        public JsonResult DeleteWidget(string ID)
        {
            WidgetBase widget = _widgetService.Get(ID);
            if (widget != null)
            {
                widget.CreateServiceInstance().DeleteWidget(ID);
                return Json(ID);
            }
            return Json(false);
        }

        public PartialViewResult Templates()
        {
            return PartialView(_widgetService.Get(m => m.IsTemplate == true));
        }

        [HttpPost]
        public PartialViewResult AppendWidget(WidgetBase widget)
        {
            var widgetPart = _widgetService.ApplyTemplate(widget, ControllerContext);
            if (widgetPart == null)
            {
                widgetPart = new HtmlWidget { PartialView = "Widget.HTML", HTML = "<h1 class='text-danger'><hr/>操作失败，找不到数据源，刷新页面后该消息会消失。<hr/></h1>" }.ToWidgetPart();
            }
            return PartialView("AppendWidget", new DesignWidgetViewModel(widgetPart, widget.PageID));
        }
        [HttpPost]
        public JsonResult CancelTemplate(string Id)
        {
            var widget = _widgetService.Get(Id);
            if (!widget.IsSystem)
            {
                widget.IsTemplate = false;
                if (widget.PageID.IsNotNullAndWhiteSpace() || widget.LayoutID.IsNotNullAndWhiteSpace())
                {
                    _widgetService.Update(widget);
                }
                else
                {
                    widget.CreateServiceInstance().DeleteWidget(Id);
                }
            }
            return Json(Id);
        }
        [HttpPost]
        public JsonResult ToggleClass(string ID, string clas)
        {
            var widget = _widgetService.Get(ID);
            if (widget != null)
            {
                if (widget.StyleClass.IsNotNullAndWhiteSpace() && widget.StyleClass.IndexOf(clas) >= 0)
                {
                    widget.StyleClass = widget.StyleClass.Replace(clas, "").Trim();
                }
                else
                {
                    widget.StyleClass = clas + " " + (widget.StyleClass ?? "");
                    widget.StyleClass = widget.StyleClass.Trim();
                }
                _widgetService.Update(widget);
            }
            return Json(ID);
        }
        [HttpPost]
        public JsonResult SaveCustomStyle(string ID, string style)
        {
            var widget = _widgetService.Get(ID);
            if (widget != null)
            {
                if (style.IsNotNullAndWhiteSpace())
                {
                    widget.StyleClass = widget.CustomClass.Trim() + " style=\"{0}\"".FormatWith(style);
                    widget.StyleClass = widget.StyleClass.Trim();
                }
                else
                {
                    widget.StyleClass = widget.CustomClass;
                }
                _widgetService.Update(widget);
            }
            return Json(ID);
        }
        public FileResult Pack(string ID)
        {
            var widget = _widgetService.Get(ID);
            var widgetPackage = widget.CreateServiceInstance().PackWidget(widget) as WidgetPackage;
            return File(widgetPackage.ToFilePackage(), "Application/zip", widgetPackage.Widget.WidgetName + ".widget");
        }
        public FileResult PackDictionary(int ID, string[] filePath)
        {

            var dataDictionary = ServiceLocator.Current.GetInstance<IDataDictionaryService>().Get(ID);
            var installer = new DataDictionaryPackageInstaller();
            if (filePath != null && filePath.Any())
            {
                installer.OnPacking = () =>
                {
                    List<System.IO.FileInfo> files = new List<System.IO.FileInfo>();
                    foreach (var item in filePath)
                    {
                        files.Add(new System.IO.FileInfo(Server.MapPath(item)));
                    }
                    return files;
                };
            }

            return File(installer.Pack(dataDictionary).ToFilePackage(), "Application/zip", dataDictionary.Title + ".widget");
        }
        [HttpPost]
        public ActionResult InstallWidgetTemplate(string returnUrl)
        {
            if (Request.Files.Count > 0)
            {
                try
                {
                    Package package;
                    var installer = _packageInstallerProvider.CreateInstaller(Request.Files[0].InputStream, out package);
                    if (installer is WidgetPackageInstaller)
                    {
                        var widgetPackage = JsonConvert.DeserializeObject<WidgetPackage>(package.Content.ToString());
                        widgetPackage.Content = package.Content;
                        widgetPackage.Widget.CreateServiceInstance().InstallWidget(widgetPackage);
                    }
                    else
                    {
                        installer.Install(package.Content.ToString());
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            }
            return Redirect(returnUrl);
        }
        [HttpPost]
        public JsonResult Copy(string widgetId)
        {
            _cookie.SetValue(Const.CopyWidgetCookie, widgetId, true, true);
            return Json(new AjaxResult { Status = AjaxStatus.Normal, Message = "复制成功，请到需要的页面区域粘贴！" });
        }

        [HttpPost]
        public PartialViewResult Paste(WidgetBase widget)
        {
            widget.ID = _cookie.GetValue<string>(Const.CopyWidgetCookie);
            return AppendWidget(widget);
        }

        public ActionResult PasteAndRedirect(WidgetBase widget, string ReturnUrl)
        {
            widget.ID = _cookie.GetValue<string>(Const.CopyWidgetCookie);
            var widgetPart = _widgetService.ApplyTemplate(widget, ControllerContext);
            if (widgetPart != null)
            {
                if (ReturnUrl.IsNotNullAndWhiteSpace())
                {
                    return Redirect(ReturnUrl);
                }
                return RedirectToAction("Edit", new { widgetPart.Widget.ID, ReturnUrl });
            }
            _cookie.GetValue<string>(Const.CopyWidgetCookie, true);
            return RedirectToAction("SelectWidget", "WidgetTemplate", new { widget.PageID, widget.ZoneID, widget.LayoutID, ReturnUrl });
        }
    }
}
