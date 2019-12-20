using System;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;


namespace ADMS_API.Controllers
{
    //configurar la ruta que se desea
    [ApiController]
    [Route("controller")]
    public class ADMSController : ControllerBase
    {   
        private readonly ILogger<ADMSController> _logger;
        public ADMSController(ILogger<ADMSController> logger){ _logger = logger; }
        //[ThreadStatic]
        //public static Database.Database database;// = new Database.Database();
        //public Database.Database database;

        // GET iclock/cdata
        // ENDPOINT de inicializacion para dispositivos biometricos
        [HttpGet("state")]
        public ActionResult<string> GetCData()
        {
            var returnValue = "registry=ok";
            _logger.LogInformation("PRUEBA: " + returnValue);
            return returnValue;
            
        }

        [HttpPost("insertBiometria")]
        public ActionResult<string> PostCData()
        {            
            GetBodyData(Request.Body);            
            return "";
        }
        /*
        [HttpPost("insertConciliadorBio")]
        public ActionResult<string> PostConciliadorBio()
        {
            GetBodyDataConciliador(Request.Body,true);
            return "";
        }
        [HttpPost("insertConciliadorUser")]
        public ActionResult<string> PostConciliadorUser()
        {
            GetBodyDataConciliador(Request.Body, false);
            return "";
        }
        */
        private void GetBodyData(Stream body)
        {
            using (var reader = new System.IO.StreamReader(body))
            {
                try
                {
                    _logger.LogInformation("INICIO" );
                    //database = new Database.Database();
                    string bodyOut = reader.ReadToEnd();
                    Biodata biodata = new Biodata();
                    biodata = JsonConvert.DeserializeObject<Biodata>(bodyOut);
                    string response = Database.Database.UseDatabase(_logger, biodata);
                    //string response = database.UseDatabase(_logger, biodata);
                    _logger.LogInformation("FIN DEL PROCESO CON RESPUESTA: " + response);/**/
                    return; 
                }
                catch (Exception ex)
                {
                    _logger.LogError("GetBodyData body: " + ex.Message);
                    return;
                }
            }
        }

        private void GetBodyDataConciliador(Stream body,bool bio)
        {
            using (var reader = new System.IO.StreamReader(body))
            {
                try
                {
                    _logger.LogInformation("INICIO CONCILIADOR");
                    //database = new Database.Database();
                    string bodyOut = reader.ReadToEnd();
                    string response = "";
                    if (bio) 
                    {
                        Biodata biodata = new Biodata();
                        biodata = JsonConvert.DeserializeObject<Biodata>(bodyOut);
                        response = Database.Database.UseDatabaseConciliador(_logger, biodata, null);
                    }
                    else 
                    {
                        Userinfo userinfo = new Userinfo();
                        userinfo = JsonConvert.DeserializeObject<Userinfo>(bodyOut);
                        response = Database.Database.UseDatabaseConciliador(_logger, null, userinfo);
                    }
                    
                    //string response = database.UseDatabase(_logger, biodata);
                    _logger.LogInformation("FIN DEL PROCESO CON RESPUESTA DEL CONCILIADOR: " + response);/**/
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError("GetBodyData body: " + ex.Message);
                    return;
                }
            }
        }
    }
    public class Biodata
    {
        public string sn{ get; set; }
        public string dni { get; set; }
        public string huella { get; set; }
        public string indiceDedo { get; set; }
        public string largoHuella { get; set; }
        public string cara { get; set; }
        public string faceId { get; set; }
        public string faceLong { get; set; }
    }

    public class Userinfo
    { 
        public string pin { get; set; }
        public string name { get; set; }
        public string pri { get; set; }
        public string passwd { get; set; }
        public string card { get; set; }
        public string rut { get; set; }
        
    }
}
