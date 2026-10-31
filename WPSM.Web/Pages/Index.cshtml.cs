using Microsoft.AspNetCore.Mvc.RazorPages;

namespace WPSM.Web.Pages
{
    public class IndexModel : PageModel
    {
        public Microsoft.AspNetCore.Mvc.IActionResult OnGet() => Redirect("/G01");
    }
}
