using Microsoft.AspNetCore.Mvc;

namespace TradingBot.Controllers;

[Route("[controller]")]
public class TestController : ControllerBase
{
    /// <summary> A test method </summary>
    /// <response code="200">Some text</response>
    [HttpGet]
    public ActionResult<string> Get() => "Hello world!";
}
