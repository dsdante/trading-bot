using Microsoft.AspNetCore.Mvc;

namespace TradingBot.Controllers;

[Route("[controller]")]
public class TestController : ControllerBase
{
    [HttpGet]
    public ActionResult<string> Get() => "Hello world!";
}
