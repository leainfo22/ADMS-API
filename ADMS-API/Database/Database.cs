using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using ADMS_API.Controllers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;


namespace ADMS_API.Database
{
    public class JsonUserInfo
    {
        public string dni;
        public string nombre;
        public string rut_emp;
        public string nombre_emp;
        public string direccion_emp;
        public string privilegio;
        public string tarjeta;
        public string grupo;
        public string pass;       
    }
    public class JsonBiophoto
    {
        public string dni;
        public string cara;
        public string indice_cara;
        public string largo_cara;
    }
    public class JsonFinger
    {
        public string dni;
        public string huella;
        public string indice_dedo;
        public string largo_huella;
    }
    public class Database
    {
        private const string SQL_CONNECTION_PRODUCTIONS = "initial catalog=Produccion; Data Source= keycloud-prod.database.windows.net; Connection Timeout=30; User Id = appkey; Password=Kkdbc36de$; Min Pool Size=20; Max Pool Size=200; MultipleActiveResultSets=True;";
        private const string SQL_CONNECTION_TRANSACTIONS = "initial catalog=Transacciones; Data Source= keycloud-prod.database.windows.net; Connection Timeout=30; User Id = appkey; Password=Kkdbc36de$; Min Pool Size=20; Max Pool Size=200; MultipleActiveResultSets=True;";
        private const int imgSize = 150000; //LARGO MAXIMO DE EL STRING DE LA IMAGEN

        private const int CODIGO_ERROR = -1;
        private const int CODIGO_SIN_DATOS = 0;
        private const int CODIGO_EXITO = 1;

        [ThreadStatic] public static string biometria = "";
        [ThreadStatic] public static string indice = "";
        [ThreadStatic] public static string largo = "";
        [ThreadStatic] public static int tipoBiometria = -1;
        [ThreadStatic] public static string indiceGetPlantilla = "";
        [ThreadStatic] public static string biometriaGetPlantilla = "";

        [ThreadStatic] private static bool isUpdate;

        [ThreadStatic] public static SqlDataReader response;
        [ThreadStatic] public static SqlDataReader response2;

        [ThreadStatic] public static Dictionary<string, string> responseFindColab;
        [ThreadStatic] public static Dictionary<string, string> responseGetInfoDisp;
        [ThreadStatic] public static List<string> listSN;

        [ThreadStatic] public static JsonUserInfo jsonUserInfo;
        [ThreadStatic] public static JsonBiophoto jsonBiophoto;
        [ThreadStatic] public static JsonFinger jsonFinger;

        [ThreadStatic] public static int disTipoOut;
        [ThreadStatic] public static bool succes;

        public static string ActualizarEstadoDelDispositivo(ILogger logger, Estado estado)
        {
            string retMsg = "";

            try
            {
                string query = "";

                string instanciaDispositivo = "";
                string sucursalDispositivo = "";

                string estadoDispositivo = "";

                using (SqlConnection connection = new SqlConnection(SQL_CONNECTION_PRODUCTIONS))
                {
                    connection.Open();                    

                    query = string.Format("SELECT INSTANCIA, ID_SUCURSAL FROM MACHINES WHERE sn = '{0}'", estado.sn);
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        SqlDataReader response;
                        response = command.ExecuteReader();
                        if (response.HasRows)
                        {
                            if (response.Read())
                            {
                                instanciaDispositivo = response[0].ToString();
                                sucursalDispositivo = response[1].ToString();
                            }
                            response.Close();
                        }
                        else
                        {
                            logger.LogError("ERROR: " + query + " SIN DATOS.");
                            response.Close();
                            return retMsg = query + " SIN DATOS.";
                        }
                    }
                    connection.Close();
                }
                using (SqlConnection connection = new SqlConnection(SQL_CONNECTION_TRANSACTIONS))
                {
                    connection.Open();

                    query = string.Format("SELECT EST_ESTADO AS estado FROM ESTADO_DISPOSITIVOS WHERE EST_SN = '{0}'", estado.sn);
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        SqlDataReader response;
                        response = command.ExecuteReader();
                        if (response.HasRows)
                        {                           
                                if (response.Read())
                                    estadoDispositivo = response[0].ToString();
                                response.Close();
                                float timezone;
                                bool success = float.TryParse(estado.timezone, out timezone);
                                if(success)
                                    query = string.Format("UPDATE ESTADO_DISPOSITIVOS SET EST_ESTADO = 1 ,EST_ULTIMO_REPORTE = CONVERT(DATETIME, '{0}', 120)", DateTime.UtcNow.AddHours(timezone).ToString("yyyy-MM-dd HH:mm:ss"));
                                else
                                    query = string.Format("UPDATE ESTADO_DISPOSITIVOS SET EST_ESTADO = 1 ,EST_ULTIMO_REPORTE = CONVERT(DATETIME, '{0}', 120)", DateTime.UtcNow.AddHours(-3).ToString("yyyy-MM-dd HH:mm:ss"));

                                if (!string.IsNullOrEmpty(sucursalDispositivo))
                                    query += string.Format(",EST_SUCURSAL = {0}", sucursalDispositivo);
                                if (!string.IsNullOrEmpty(estado.ip))
                                    query += string.Format(",EST_IP = '{0}'", estado.ip);
                                if (!string.IsNullOrEmpty(estado.host))
                                    query += string.Format(",EST_HOST = '{0}'", estado.host);
                                if (!string.IsNullOrEmpty(estado.fw))
                                    query += string.Format(",EST_VERSION_FW = '{0}'", estado.fw);
                                if (!string.IsNullOrEmpty(estado.usuarios))
                                    query += string.Format(",EST_CANT_USUARIOS = {0}", estado.usuarios);
                                if (!string.IsNullOrEmpty(estado.huellas))
                                    query += string.Format(",EST_CANT_HUELLAS = {0}", estado.huellas);
                                if (!string.IsNullOrEmpty(estado.marcas))
                                    query += string.Format(",EST_CANT_MARCAS = {0}", estado.marcas);
                                if (!string.IsNullOrEmpty(estado.rostros))
                                    query += string.Format(",EST_CANT_ROSTROS = {0}", estado.rostros);
                                if (!string.IsNullOrEmpty(estado.ver_huella))
                                    query += string.Format(",EST_VERSION_ALGORITMO_HUELLA = {0}", estado.ver_huella);
                                if (!string.IsNullOrEmpty(estado.ver_rostro))
                                    query += string.Format(",EST_VERSION_ALGORITMO_ROSTRO = {0}", estado.ver_rostro);
                                if (!string.IsNullOrEmpty(estado.cant_funciones))
                                    query += string.Format(",EST_CANT_FUNCIONES_SOPORTADAS = {0}", estado.cant_funciones);
                                if (!string.IsNullOrEmpty(estado.cant_rostros_enrolamiento))
                                    query += string.Format(",EST_CANT_ROSTROS_ENROLAMIENTO = {0}", estado.cant_rostros_enrolamiento);
                                query += string.Format(",EST_ESTADO_CARGA = 'NO_APLICA',EST_BATERIA_RESTANTE='NO_APLICA',EST_TEAM_VIEWER_ID='NO_APLICA' WHERE EST_SN = '{0}'", estado.sn);

                            using (SqlCommand command1 = new SqlCommand(query, connection)) { command1.ExecuteNonQuery(); }
                            retMsg = "ESTADO ACTUALIZADO " + estado.sn;                                
                        }
                        else
                        {
                            float timezone;
                            bool success = float.TryParse(estado.timezone, out timezone);
                            if (!success)
                                timezone = -3;
                            response.Close();
                            query = "INSERT INTO ESTADO_DISPOSITIVOS (EST_SN,EST_ESTADO,EST_ULTIMO_REPORTE,EST_SUCURSAL,EST_IP,EST_HOST,EST_VERSION_FW,EST_CANT_USUARIOS,";
                            query += "EST_CANT_HUELLAS,EST_CANT_MARCAS,EST_CANT_ROSTROS,EST_VERSION_ALGORITMO_HUELLA,EST_VERSION_ALGORITMO_ROSTRO,EST_CANT_FUNCIONES_SOPORTADAS,EST_CANT_ROSTROS_ENROLAMIENTO,";
                            query += "EST_ESTADO_CARGA,EST_BATERIA_RESTANTE,EST_TEAM_VIEWER_ID)";
                            query += string.Format("VALUES('{0}', 1, CONVERT(DATETIME, '{16}', 120), '{1}', '{2}', '{3}', '{4}',{5},{6},{7},{8},{9},{10},{11},{12},'{13}','{14}','{15}')", estado.sn,sucursalDispositivo,estado.ip,estado.host,estado.fw,estado.usuarios,estado.huellas,0,estado.rostros,estado.ver_huella,estado.ver_rostro,estado.cant_funciones,estado.cant_rostros_enrolamiento,"NO_APLICA", "NO_APLICA", "NO_APLICA", DateTime.UtcNow.AddHours(timezone).ToString("yyyy-MM-dd HH:mm:ss"));
                            using (SqlCommand command1 = new SqlCommand(query, connection)) { command1.ExecuteNonQuery(); }

                            retMsg = "ESTADO INSERTADO " + estado.sn;
                        }
                    }
                    connection.Close();
                }
            }
            catch (Exception ex) 
            {
                logger.LogError("ERROR: " + ex.Message + " TRACE "+ ex.StackTrace);
                return retMsg = ex.Message;
            }          

            return retMsg;
        }

        public static string ActualizarEstadoDelDispositivoLite(ILogger logger, EstadoLite estado) 
        {
            string retMsg = "";

            try
            {
                string query = "";
                string instanciaDispositivo = "";
                string sucursalDispositivo = "";
                string estadoDispositivo = "";

                using (SqlConnection connection = new SqlConnection(SQL_CONNECTION_PRODUCTIONS))
                {
                    connection.Open();

                    query = string.Format("SELECT INSTANCIA, ID_SUCURSAL FROM MACHINES WHERE sn = '{0}'", estado.sn);
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        SqlDataReader response;
                        response = command.ExecuteReader();
                        if (response.HasRows)
                        {
                            if (response.Read())
                            {
                                instanciaDispositivo = response[0].ToString();
                                sucursalDispositivo = response[1].ToString();
                            }
                            response.Close();
                        }
                        else
                        {
                            logger.LogError("ERROR: " + query + " SIN DATOS.");
                            response.Close();
                            return retMsg = query + " SIN DATOS.";
                        }
                    }
                    connection.Close();
                }
                using (SqlConnection connection = new SqlConnection(SQL_CONNECTION_TRANSACTIONS))
                {
                    connection.Open();

                    query = string.Format("SELECT EST_ESTADO AS estado FROM ESTADO_DISPOSITIVOS WHERE EST_SN = '{0}'", estado.sn);
                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        SqlDataReader response;
                        response = command.ExecuteReader();
                        if (response.HasRows)
                        {
                            if (response.Read())
                                estadoDispositivo = response[0].ToString();
                            response.Close();
                            float timezone;
                            bool success = float.TryParse(estado.timezone, out timezone);
                            if (success)
                                query = string.Format("UPDATE ESTADO_DISPOSITIVOS SET EST_ESTADO = 1 ,EST_ULTIMO_REPORTE = CONVERT(DATETIME, '{0}', 120)", DateTime.UtcNow.AddHours(timezone).ToString("yyyy-MM-dd HH:mm:ss"));
                            else
                                query = string.Format("UPDATE ESTADO_DISPOSITIVOS SET EST_ESTADO = 1 ,EST_ULTIMO_REPORTE = CONVERT(DATETIME, '{0}', 120)", DateTime.UtcNow.AddHours(-3).ToString("yyyy-MM-dd HH:mm:ss"));
                            query += string.Format(",EST_HOST = '{0}'", estado.host);
                            query += string.Format(",EST_ESTADO_CARGA = 'NO_APLICA',EST_BATERIA_RESTANTE='NO_APLICA',EST_TEAM_VIEWER_ID='NO_APLICA' WHERE EST_SN = '{0}'", estado.sn);

                            using (SqlCommand command1 = new SqlCommand(query, connection)) { command1.ExecuteNonQuery(); }
                            retMsg = "ESTADO ACTUALIZADO LITE " + estado.sn;
                        }                       
                        response.Close();
                        
                    }
                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                logger.LogError("ERROR: " + ex.Message + " TRACE " + ex.StackTrace);
                return retMsg = ex.Message;
            }

            return retMsg;
        }              
    
    }
}