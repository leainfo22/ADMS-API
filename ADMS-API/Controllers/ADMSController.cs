using System;
using System.Collections.Generic;
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
            var returnValue = "ESTADO BIOMETRICO: OK";
            _logger.LogInformation("PRUEBA: " + returnValue);

            return returnValue;            
        }         

        [HttpPost("estadoDispositivo")]
        public ActionResult<string> EstadoBiometico() 
        {
            GetBodyDataEstado(Request.Body);
            return "";
        }
        [HttpPost("estadoDispositivoLite")]
        public ActionResult<string> EstadoBiometicoLite()
        {
            GetBodyDataEstadoLite(Request.Body);
            return "";
        } 

        private void GetBodyDataEstado(Stream body)
        {
            try
            {
                using (var reader = new System.IO.StreamReader(body))
                {                
                    _logger.LogInformation("INICIO ESTADO DISPOSITIVO");
                    string bodyOut = reader.ReadToEnd();

                    Estado estado = new Estado();
                    estado = JsonConvert.DeserializeObject<Estado>(bodyOut);
                    string response = Database.Database.ActualizarEstadoDelDispositivo(_logger, estado);
                    _logger.LogInformation("FIN DEL PROCESO CON RESPUESTA: " + response);
                    return;          
                
                } 
            }
            catch (Exception ex)
            {
                _logger.LogError("GetBodyDataEstado: " + ex.Message +" TRACE: " + ex.StackTrace);
                return;
            }
        }

        private void GetBodyDataEstadoLite(Stream body)
        {
            try
            {
                using (var reader = new System.IO.StreamReader(body))
                {
                    _logger.LogInformation("INICIO ESTADO DISPOSITIVO");
                    string bodyOut = reader.ReadToEnd();

                    EstadoLite estado = new EstadoLite();
                    estado = JsonConvert.DeserializeObject<EstadoLite>(bodyOut);
                    string response = Database.Database.ActualizarEstadoDelDispositivoLite(_logger, estado);
                    _logger.LogInformation("FIN DEL PROCESO CON RESPUESTA: " + response);
                    return;

                }
            }
            catch (Exception ex)
            {
                _logger.LogError("GetBodyDataEstado: " + ex.Message + " TRACE: " + ex.StackTrace);
                return;
            }
        }
    }
    public class Estado
    {
        public string sn { get; set; }
        public string ip { get; set; }
        public string host { get; set; }
        public string fw { get; set; }
        public string usuarios { get; set; }
        public string huellas { get; set; }
        public string marcas { get; set; }
        public string rostros { get; set; }
        public string ver_huella { get; set; }
        public string ver_rostro { get; set; }
        public string cant_funciones { get; set; }
        public string cant_rostros_enrolamiento { get; set; }
        public string timezone { get; set; }

    }

    public class EstadoLite
    {
        public string sn { get; set; }
        public string host { get; set; }
        public string timezone { get; set; }

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
        public string sn { get; set; }
        public IList<datosColab> datos_colab { get; set; }       
    }

    public class datosColab
    {
        public string userId { get; set; }
        public string nombre { get; set; }
        public string tarjeta { get; set; }
        public string clave { get; set; }
        public string privilegio { get; set; }
        public string run { get; set; }

    }
}
