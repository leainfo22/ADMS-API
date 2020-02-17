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
        //[ThreadStatic] public static int respFindColab;
        //[ThreadStatic] public static int responseGetPlantilla;
        //[ThreadStatic] public static int responselookForPreEnrol;
        //[ThreadStatic] public static int responseCheckModelo;
        [ThreadStatic] public static bool succes;
        
        //[ThreadStatic] public static int res;

        public static string UseDatabase(ILogger logger, Biodata biodata)
        {

            if (!string.IsNullOrEmpty(biodata.huella) && biodata.huella != "")
            {
                biometria = biodata.huella;
                indice = biodata.indiceDedo;
                largo = biodata.largoHuella;
                tipoBiometria = 1;
            }
            else if (!string.IsNullOrEmpty(biodata.cara) && biodata.cara != "")
            {
                biometria = biodata.cara;
                indice = biodata.faceId;
                largo = biodata.faceLong;
                tipoBiometria = 2;
            }
            else
                return "ERROR BIOMETRIA VACIA.";

            using (SqlConnection connection = new SqlConnection(SQL_CONNECTION_PRODUCTIONS))
            {
                connection.Open();
                responseGetInfoDisp = new Dictionary<string, string>();
                responseFindColab = new Dictionary<string, string>();
            
                if (getInfoDisp(connection, logger, biodata.sn) != CODIGO_EXITO)
                    return "Error en getInfoDisp, dispositivo sin datos o error en query";

                bool success = Int32.TryParse(responseGetInfoDisp["dis_tipo"], out disTipoOut);

                int respFindColab = findColab(connection, logger, biodata.dni, responseGetInfoDisp["instancia"]);
                if (respFindColab == CODIGO_SIN_DATOS) //SIN DATOS
                {
                    int responselookForPreEnrol = lookForPreEnrol(connection, logger, biodata.dni, responseGetInfoDisp["sucursal"]);
                    if (responselookForPreEnrol == CODIGO_SIN_DATOS || responselookForPreEnrol == CODIGO_EXITO)
                    {
                        if (responselookForPreEnrol == CODIGO_SIN_DATOS)
                        {
                            logger.LogInformation("Se agrega usuario a la tabla de pre-enrolamiento");
                            if (addDniPreEnrol(connection, logger, biodata.dni, biodata.sn, "", responseGetInfoDisp["sucursal"]) != CODIGO_EXITO)
                                return "CODIGO DE ERROR EN addDniPreEnrol.";                        
                        }

                        int responseGetPlantilla = getPlantilla(connection, logger, biodata.dni, indice, tipoBiometria, disTipoOut);
                        if (responseGetPlantilla == CODIGO_SIN_DATOS)
                        {
                            isUpdate = false;
                            if (createOrUpdateTemplate(connection, logger, biodata.dni, biometria, indice, tipoBiometria, largo, disTipoOut, responseGetPlantilla) != CODIGO_EXITO)
                                return "CODIGO DE ERROR EN createOrUpdateTemplate.";
                            return "Template creado.";
                        }
                        else if (responseGetPlantilla == CODIGO_EXITO)
                        {
                            string msgResponse = "";
                            if (indiceGetPlantilla == indice) 
                            {
                                isUpdate = true; 
                                msgResponse = "Template actualizado."; 
                            }
                            else 
                            { 
                                isUpdate = false; 
                                msgResponse = "Template actualizado."; 
                            }

                            if (createOrUpdateTemplate(connection, logger, biodata.dni, biometria, indice, tipoBiometria, largo, disTipoOut, responseGetPlantilla) != CODIGO_EXITO)
                                return "CODIGO DE ERROR EN createOrUpdateTemplate.";
                            return msgResponse;
                        }
                        else
                            return "CODIGO DE ERROR EN GetPlantilla.";
                    }
                    else                    
                        return "CODIGO DE ERROR EN lookForPreEnrol.";
                    
                }
                else if (respFindColab == CODIGO_EXITO)// EXITO
                {
                    listSN = new List<string>();
                    int responseGetPlantilla = getPlantilla(connection, logger, biodata.dni, indice, tipoBiometria, disTipoOut);
                    if (responseGetPlantilla == CODIGO_SIN_DATOS)
                    {
                        isUpdate = false;
                        if (createOrUpdateTemplate(connection, logger, biodata.dni, biometria, indice, tipoBiometria, largo, disTipoOut, responseGetPlantilla) == CODIGO_EXITO)
                        {
                            int responseCheckModelo = checkModelo(connection, logger, biodata.dni, responseGetInfoDisp["zona"], responseGetInfoDisp["instancia"], responseGetInfoDisp["id"]);
                            getDispsModelo(connection, logger, biodata.dni, responseGetInfoDisp["id"], responseGetInfoDisp["instancia"], responseGetInfoDisp["sucursal"]);

                            if (responseCheckModelo == CODIGO_EXITO)
                            {
                                addPublish(logger, biodata.sn, biodata.dni, responseFindColab["nom"], responseFindColab["rut_emp"], responseFindColab["nom_emp"], responseFindColab["dir_emp"], responseFindColab["pZk"], responseFindColab["tarjeta"], "1", biometria, indice, largo, tipoBiometria.ToString(), "", listSN);
                                return "Biometria enviada a la tabla transacciones.";                                                                    
                            }
                            else if (responseCheckModelo == CODIGO_SIN_DATOS)
                            {
                                int res = addKeyModelD(connection, logger,"0", biodata.dni,biometria,tipoBiometria.ToString(), responseGetInfoDisp["instancia"], responseGetInfoDisp["id"],"");
                                if (res == CODIGO_EXITO) 
                                {
                                    logger.LogInformation("Se envian a transacciones la biometria asociada al usuario " + biodata.dni + " para cada dispositivo.");
                                    addPublish(logger, biodata.sn, biodata.dni, responseFindColab["nom"], responseFindColab["rut_emp"], responseFindColab["nom_emp"], responseFindColab["dir_emp"], responseFindColab["pZk"], responseFindColab["tarjeta"], "1", biometria, indice, largo, tipoBiometria.ToString(), "", listSN);
                                    return "Biometria enviada a la tabla transacciones.";                                    
                                }
                                else                               
                                    return "ERROR EN addKeyModelD.";
                                
                            }
                            else                            
                                return "ERROR EN responseCheckModelo.";                                                        
                        }
                        else                         
                            return "ERROR EN createOrUpdateTemplate.";                                              
                    }
                    else if (responseGetPlantilla == CODIGO_EXITO) 
                    {
                        if (!string.IsNullOrEmpty(biometria) || biometria != "")
                        {
                            if (indice != indiceGetPlantilla)
                                isUpdate = false;
                            else
                                isUpdate = true;

                            if (createOrUpdateTemplate(connection, logger, biodata.dni, biometria, indice, tipoBiometria, largo, disTipoOut, responseGetPlantilla) == CODIGO_EXITO)
                            {
                                int responseCheckModelo = checkModelo(connection, logger, biodata.dni, responseGetInfoDisp["zona"], responseGetInfoDisp["instancia"], responseGetInfoDisp["id"]);
                                getDispsModelo(connection, logger, biodata.dni, responseGetInfoDisp["id"], responseGetInfoDisp["instancia"], responseGetInfoDisp["sucursal"]);
                                if (responseCheckModelo == CODIGO_EXITO)
                                {
                                    addPublish(logger, biodata.sn, biodata.dni, responseFindColab["nom"], responseFindColab["rut_emp"], responseFindColab["nom_emp"], responseFindColab["dir_emp"], responseFindColab["pZk"], responseFindColab["tarjeta"], "1", biometria, indice, largo, tipoBiometria.ToString(), "", listSN);
                                    return "Biometria enviada a la tabla transacciones.";
                                }
                                else if (responseCheckModelo == CODIGO_SIN_DATOS)
                                {
                                    int res = addKeyModelD(connection, logger, "0", biodata.dni, biometria, tipoBiometria.ToString(), responseGetInfoDisp["instancia"], responseGetInfoDisp["id"], "");
                                    if (res == CODIGO_EXITO)
                                    {
                                        addPublish(logger, biodata.sn, biodata.dni, responseFindColab["nom"], responseFindColab["rut_emp"], responseFindColab["nom_emp"], responseFindColab["dir_emp"], responseFindColab["pZk"], responseFindColab["tarjeta"], "1", biometria, indice, largo, tipoBiometria.ToString(), "", listSN);
                                        return "Biometria enviada a la tabla transacciones.";
                                    }
                                    else
                                        return "ERROR EN addKeyModelD.";                                            
                                }
                                else                                        
                                    return "ERROR EN responseCheckModelo.";
                            }                           
                        }
                        else
                            return "ERROR BIOMETRIA VACIA O NULLA";
                    }
                    else 
                        return "ERROR: responseGetPlantilla";
                }
                else                
                    return " CODIGO DE ERROR EN findColab.";                
            }
            return "FIN DE USE DATABASE";
        }

        public static string UseDatabaseHash(ILogger logger, Biodata biodata)
        {
            if (!string.IsNullOrEmpty(biodata.huella) && biodata.huella != "")
            {
                biometria = biodata.huella;
                indice = biodata.indiceDedo;
                largo = biodata.largoHuella;
                tipoBiometria = 1;
            }
            else if (!string.IsNullOrEmpty(biodata.cara) && biodata.cara != "")
            {
                biometria = biodata.cara;
                indice = biodata.faceId;
                largo = biodata.faceLong;
                tipoBiometria = 2;
            }
            else
                return "ERROR BIOMETRIA VACIA.";

            using (SqlConnection connection = new SqlConnection(SQL_CONNECTION_PRODUCTIONS))
            {
                connection.Open();
                responseGetInfoDisp = new Dictionary<string, string>();
                responseFindColab = new Dictionary<string, string>();

                if (getInfoDisp(connection, logger, biodata.sn) != CODIGO_EXITO)
                    return "Error en getInfoDisp, dispositivo sin datos o error en query";

                bool success = Int32.TryParse(responseGetInfoDisp["dis_tipo"], out disTipoOut);

                int respFindColab = findColab(connection, logger, biodata.dni, responseGetInfoDisp["instancia"]);
                if (respFindColab == CODIGO_SIN_DATOS) //SIN DATOS
                {
                    int responselookForPreEnrol = lookForPreEnrol(connection, logger, biodata.dni, responseGetInfoDisp["sucursal"]);
                    if (responselookForPreEnrol == CODIGO_SIN_DATOS || responselookForPreEnrol == CODIGO_EXITO)
                    {
                        if (responselookForPreEnrol == CODIGO_SIN_DATOS)
                        {
                            logger.LogInformation("Se agrega usuario a la tabla de pre-enrolamiento");
                            if (addDniPreEnrol(connection, logger, biodata.dni, biodata.sn, "", responseGetInfoDisp["sucursal"]) != CODIGO_EXITO)
                                return "CODIGO DE ERROR EN addDniPreEnrol.";
                        }

                        int responseGetPlantilla = getPlantilla(connection, logger, biodata.dni, indice, tipoBiometria, disTipoOut);
                        if (responseGetPlantilla == CODIGO_SIN_DATOS)
                        {
                            return "ALERTA LA PERSONA NO TIENE DATOS BIOMETRICOS EN LA DB PERO SI EN EL EQUIPO.";
                        }
                        else if (responseGetPlantilla == CODIGO_EXITO)
                        {
                            return "EN ESTA API NO SE ACTUALIZA NADA DESDE EL EQUIPO ZK";
                        }
                        else
                            return "CODIGO DE ERROR EN GetPlantilla.";
                    }
                    else
                        return "CODIGO DE ERROR EN lookForPreEnrol.";

                }
                else if (respFindColab == CODIGO_EXITO)// EXITO
                {
                    listSN = new List<string>();
                    int responseGetPlantilla = getPlantilla(connection, logger, biodata.dni, indice, tipoBiometria, disTipoOut);
                    if (responseGetPlantilla == CODIGO_SIN_DATOS)
                    {
                        return "ALERTA SE EL USUARIO TIENE BIOMETRIA QUE NO ESTA EN LA DB";
                    }
                    else if (responseGetPlantilla == CODIGO_EXITO)
                    {
                        if (!string.IsNullOrEmpty(biometria) || biometria != "")
                        {
                            if (indice != indiceGetPlantilla)
                                isUpdate = false;
                            else
                                isUpdate = true;

                            if (createOrUpdateTemplate(connection, logger, biodata.dni, biometria, indice, tipoBiometria, largo, disTipoOut, responseGetPlantilla) == CODIGO_EXITO)
                            {
                                int responseCheckModelo = checkModelo(connection, logger, biodata.dni, responseGetInfoDisp["zona"], responseGetInfoDisp["instancia"], responseGetInfoDisp["id"]);
                                getDispsModelo(connection, logger, biodata.dni, responseGetInfoDisp["id"], responseGetInfoDisp["instancia"], responseGetInfoDisp["sucursal"]);
                                if (responseCheckModelo == CODIGO_EXITO)
                                {
                                    addPublish(logger, biodata.sn, biodata.dni, responseFindColab["nom"], responseFindColab["rut_emp"], responseFindColab["nom_emp"], responseFindColab["dir_emp"], responseFindColab["pZk"], responseFindColab["tarjeta"], "1", biometria, indice, largo, tipoBiometria.ToString(), "", listSN);
                                    return "Biometria enviada a la tabla transacciones.";
                                }
                                else if (responseCheckModelo == CODIGO_SIN_DATOS)
                                {
                                    int res = addKeyModelD(connection, logger, "0", biodata.dni, biometria, tipoBiometria.ToString(), responseGetInfoDisp["instancia"], responseGetInfoDisp["id"], "");
                                    if (res == CODIGO_EXITO)
                                    {
                                        addPublish(logger, biodata.sn, biodata.dni, responseFindColab["nom"], responseFindColab["rut_emp"], responseFindColab["nom_emp"], responseFindColab["dir_emp"], responseFindColab["pZk"], responseFindColab["tarjeta"], "1", biometria, indice, largo, tipoBiometria.ToString(), "", listSN);
                                        return "Biometria enviada a la tabla transacciones.";
                                    }
                                    else
                                        return "ERROR EN addKeyModelD.";
                                }
                                else
                                    return "ERROR EN responseCheckModelo.";
                            }
                        }
                        else
                            return "ERROR BIOMETRIA VACIA O NULLA";
                    }
                    else
                        return "ERROR: responseGetPlantilla";
                }
                else
                    return " CODIGO DE ERROR EN findColab.";
            }
            return "FIN DE USE DATABASE";
        }

        public string UseDatabaseConciliador(ILogger logger, Biodata biodata, Userinfo userinfo)
        {
            try
            {
                using (SqlConnection connection = new SqlConnection(SQL_CONNECTION_PRODUCTIONS))
                {
                    string dni = "";
                    string sn = "";

                    if (userinfo != null)
                    {
                        dni = userinfo.datos_colab[0].userId;
                        sn = userinfo.sn;
                    }
                    else if (biodata != null)
                    {
                        dni = biodata.dni;
                        sn = biodata.sn;
                    }
                    else 
                    {
                        logger.LogError("ERROR CONCILIADOR: " + " BIODATA O USERINFO NULL");
                        return "";
                    }
                    connection.Open();
                    string query = string.Format("SELECT 1 FROM KEY_CONCILIADOR WHERE DNI ='{0}'", dni); // seleccionar todos los bagnumer sucursal 

                    using (SqlCommand command = new SqlCommand(query, connection))
                    {
                        logger.LogInformation("API_CONCILIADOR getInfoDisp - QUERY: " + query);
                        response = command.ExecuteReader();
                        try
                        {
                            if (response.HasRows)
                            {
                                response.Close();
                                if (userinfo != null)
                                {
                                    query = string.Format("UPDATE KEY_CONCILIADOR SET FECHA = CONVERT(DATETIME, '{0}'), USERINFO = {1} WHERE DNI = '{2}' AND SN = '{3}'", DateTime.Now.ToString("yyyy-MM-dd"), 1, dni, sn);
                                    using (SqlCommand command2 = new SqlCommand(query, connection))
                                    {
                                        logger.LogInformation("API_CONCILIADOR getInfoDisp - QUERY: " + query);
                                        command2.ExecuteNonQuery();
                                    }
                                    return "USERINFO ACTUALIZADA.";
                                }
                                else if (!string.IsNullOrEmpty(biodata.cara) && biodata.cara != "")
                                {
                                    query = string.Format("UPDATE KEY_CONCILIADOR SET FECHA = CONVERT(DATETIME, '{0}'), BIOPHOTO = {1}, HASHBIOPHOTO = '{2}' WHERE DNI = '{3}' AND SN = '{4}' ", DateTime.Now.ToString("yyyy-MM-dd"), 1, ComputeSha256Hash(biodata.cara + sn), dni, sn);
                                    using (SqlCommand command2 = new SqlCommand(query, connection))
                                    {
                                        logger.LogInformation("API_CONCILIADOR getInfoDisp - QUERY: " + query);
                                        command2.ExecuteNonQuery();
                                    }
                                    return "BIOPHOTO ACTUALIZADA.";
                                }
                                else if (!string.IsNullOrEmpty(biodata.huella) && biodata.huella != "")
                                {
                                    string res = "";
                                    switch (biodata.indiceDedo)
                                    {
                                        case "0":
                                            query = string.Format("UPDATE KEY_CONCILIADOR SET FECHA = CONVERT(DATETIME, '{0}'), BIODATA = {1}, HASHBIODATA = '{2}' WHERE DNI = '{3}' AND SN = '{4}'", DateTime.Now.ToString("yyyy-MM-dd"), 1, ComputeSha256Hash(biodata.huella + sn), dni, sn);
                                            using (SqlCommand command2 = new SqlCommand(query, connection))
                                            {
                                                logger.LogInformation("API_CONCILIADOR getInfoDisp - QUERY: " + query);
                                                command2.ExecuteNonQuery();
                                            }
                                            res = "BIODATA_0 ACTUALIZADA.";
                                            break;
                                        case "1":
                                            query = string.Format("UPDATE KEY_CONCILIADOR SET FECHA = CONVERT(DATETIME, '{0}'), BIODATA1 = {1}, HASHBIODATA1 = '{2}' WHERE DNI = '{3}' AND SN = '{4}'", DateTime.Now.ToString("yyy-MM-dd"), 1, ComputeSha256Hash(biodata.huella + sn), dni, sn);
                                            using (SqlCommand command2 = new SqlCommand(query, connection))
                                            {
                                                logger.LogInformation("API_CONCILIADOR getInfoDisp - QUERY: " + query);
                                                command2.ExecuteNonQuery();
                                            }
                                            res = "BIODATA_1 ACTUALIZADA.";
                                            break;
                                        case "2":
                                            query = string.Format("UPDATE KEY_CONCILIADOR SET FECHA = CONVERT(DATETIME, '{0}'), BIODATA2 = {1}, HASHBIODATA2 = '{2}' WHERE DNI = '{3}' AND SN = '{4}'", DateTime.Now.ToString("yyyy-MM-dd"), 1, ComputeSha256Hash(biodata.huella + sn), dni, sn);
                                            using (SqlCommand command2 = new SqlCommand(query, connection))
                                            {
                                                logger.LogInformation("API_CONCILIADOR getInfoDisp - QUERY: " + query);
                                                command2.ExecuteNonQuery();
                                            }
                                            res = "BIODATA_2 ACTUALIZADA.";
                                            break;
                                        case "3":
                                            query = string.Format("UPDATE KEY_CONCILIADOR SET FECHA = CONVERT(DATETIME, '{0}'), BIODATA3 = {1}, HASHBIODATA3 = '{2}' WHERE DNI = '{3}' AND SN = '{4}'", DateTime.Now.ToString("yyyy-MM-dd"), 1, ComputeSha256Hash(biodata.huella + sn), dni, sn);
                                            using (SqlCommand command2 = new SqlCommand(query, connection))
                                            {
                                                logger.LogInformation("API_CONCILIADOR getInfoDisp - QUERY: " + query);
                                                command2.ExecuteNonQuery();
                                            }
                                            res = "BIODATA_3 ACTUALIZADA.";
                                            break;
                                        case "4":
                                            query = string.Format("UPDATE KEY_CONCILIADOR SET FECHA = CONVERT(DATETIME, '{0}'), BIODATA4 = {1}, HASHBIODATA4 = '{2}' WHERE DNI = '{3}' AND SN = '{4}'", DateTime.Now.ToString("yyyy-MM-dd"), 1, ComputeSha256Hash(biodata.huella + sn), dni, sn);
                                            using (SqlCommand command2 = new SqlCommand(query, connection))
                                            {
                                                logger.LogInformation("API_CONCILIADOR getInfoDisp - QUERY: " + query);
                                                command2.ExecuteNonQuery();
                                            }
                                            res = "BIODATA_4 ACTUALIZADA.";
                                            break;
                                        case "5":
                                            query = string.Format("UPDATE KEY_CONCILIADOR SET FECHA = CONVERT(DATETIME, '{0}'), BIODATA5 = {1}, HASHBIODATA5 = '{2}' WHERE DNI = '{3}' AND SN = '{4}'", DateTime.Now.ToString("yyyy-MM-dd"), 1, ComputeSha256Hash(biodata.huella + sn), dni, sn);
                                            using (SqlCommand command2 = new SqlCommand(query, connection))
                                            {
                                                logger.LogInformation("API_CONCILIADOR getInfoDisp - QUERY: " + query);
                                                command2.ExecuteNonQuery();
                                            }
                                            res = "BIODATA_5 ACTUALIZADA.";
                                            break;
                                        case "6":
                                            query = string.Format("UPDATE KEY_CONCILIADOR SET FECHA = CONVERT(DATETIME, '{0}'), BIODATA6 = {1}, HASHBIODATA6 = '{2}' WHERE DNI = '{3}' AND SN = '{4}'", DateTime.Now.ToString("yyyy-MM-dd"), 1, ComputeSha256Hash(biodata.huella + sn), dni, sn);
                                            using (SqlCommand command2 = new SqlCommand(query, connection))
                                            {
                                                logger.LogInformation("API_CONCILIADOR getInfoDisp - QUERY: " + query);
                                                command2.ExecuteNonQuery();
                                            }
                                            res = "BIODATA_6 ACTUALIZADA.";
                                            break;
                                        case "7":
                                            query = string.Format("UPDATE KEY_CONCILIADOR SET FECHA = CONVERT(DATETIME, '{0}'), BIODATA7 = {1}, HASHBIODATA7 = '{2}' WHERE DNI = '{3}' AND SN = '{4}'", DateTime.Now.ToString("yyyy-MM-dd"), 1, ComputeSha256Hash(biodata.huella + sn), dni, sn);
                                            using (SqlCommand command2 = new SqlCommand(query, connection))
                                            {
                                                logger.LogInformation("API_CONCILIADOR getInfoDisp - QUERY: " + query);
                                                command2.ExecuteNonQuery();
                                            }
                                            res = "BIODATA_7 ACTUALIZADA.";
                                            break;
                                        case "8":
                                            query = string.Format("UPDATE KEY_CONCILIADOR SET FECHA = CONVERT(DATETIME, '{0}'), BIODATA8 = {1}, HASHBIODATA8 = '{2}' WHERE DNI = '{3}' AND SN = '{4}'", DateTime.Now.ToString("yyyy-MM-dd"), 1, ComputeSha256Hash(biodata.huella + sn), dni, sn);
                                            using (SqlCommand command2 = new SqlCommand(query, connection))
                                            {
                                                logger.LogInformation("API_CONCILIADOR getInfoDisp - QUERY: " + query);
                                                command2.ExecuteNonQuery();
                                            }
                                            res = "BIODATA_8 ACTUALIZADA.";
                                            break;
                                        case "9":
                                            query = string.Format("UPDATE KEY_CONCILIADOR SET FECHA = CONVERT(DATETIME, '{0}'), BIODATA9 = {1}, HASHBIODATA9 = '{2}' WHERE DNI = '{3}' AND SN = '{4}'", DateTime.Now.ToString("yyyy-MM-dd"), 1, ComputeSha256Hash(biodata.huella + sn), dni, sn);
                                            using (SqlCommand command2 = new SqlCommand(query, connection))
                                            {
                                                logger.LogInformation("API_CONCILIADOR getInfoDisp - QUERY: " + query);
                                                command2.ExecuteNonQuery();
                                            }
                                            res = "BIODATA_9 ACTUALIZADA.";
                                            break;
                                    }
                                    return res;
                                }
                            }
                            else
                            {
                                query = "INSERT INTO KEY_CONCILIADOR(DNI, SN, FECHA, USERINFO, BIOPHOTO, BIODATA, BIODATA1, BIODATA2,BIODATA3,BIODATA4,BIODATA5,BIODATA6,BIODATA7,BIODATA8,BIODATA9,HASHBIOPHOTO,HASHBIODATA,HASHBIODATA1,HASHBIODATA2,HASHBIODATA3,HASHBIODATA4,HASHBIODATA5,HASHBIODATA6,HASHBIODATA7,HASHBIODATA8,HASHBIODATA9) VALUES(";
                                query += string.Format("'{0}', '{1}', CONVERT(DATETIME, '{2}'), {3}, {4}, {5}, {6}, {7}, {8}, {9}, {10}, {11}, {12}, {13}, {14}, '{15}', '{16}', '{17}', '{18}', '{19}', '{20}', '{21}', '{22}', '{23}', '{24}', '{25}')", dni, sn, DateTime.Now.ToString("yyyy-MM-dd"), 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "0", "0", "0", "0", "0", "0", "0", "0", "0", "0", "0");
                                using (SqlCommand command2 = new SqlCommand(query, connection))
                                {
                                    logger.LogInformation("getInfoDisp - QUERY: " + query);
                                    command2.ExecuteNonQuery();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            response.Close();
                            logger.LogError("getInfoDisp - ERROR EN LA QUERY: " + ex.Message);

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError("ERROR CONCILIADOR: " + ex.Message);
                return "";
            }

            return "";
        }

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
                            query += string.Format("VALUES('{0}', 1, CONVERT(DATETIME, '{16}', 120), '{1}', '{2}', '{3}', '{4}',{5},{6},{7},{8},{9},{10},{11},{12},'{13}','{14}','{15}')", estado.sn,sucursalDispositivo,estado.ip,estado.host,estado.fw,estado.usuarios,estado.huellas,estado.marcas,estado.rostros,estado.ver_huella,estado.ver_rostro,estado.cant_funciones,estado.cant_rostros_enrolamiento,"NO_APLICA", "NO_APLICA", "NO_APLICA", DateTime.UtcNow.AddHours(timezone).ToString("yyyy-MM-dd HH:mm:ss"));
                            
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
                            retMsg = "ESTADO ACTUALIZADO " + estado.sn;
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

        public static string ComputeSha256Hash(string rawData)
        {
            // Create a SHA256   
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array  
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string   
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }


        #region METODOS A OCUPAR EN UseDatabase
        public static int addKeyModelD(SqlConnection connection, ILogger logger, string zona, string dni, string biometria, string tipoBiometria, string instancia, string idDisp, string pass)
        {

            string huella, cara;
            if (string.IsNullOrEmpty(zona) || zona == "")
                zona = "0";            

            if (string.IsNullOrEmpty(biometria) || biometria == "" )
            {
                huella = "0";
                cara = "0";
            }
            else
            {
                if (tipoBiometria == "1") 
                {
                    huella = "1";
                    cara = "0";
                } else
                {
                    cara = "1";
                    huella = "0";
                }
            }

            if (string.IsNullOrEmpty(pass) || pass == "")            
                pass = "0";
            else           
                pass = "1";

            if (string.IsNullOrEmpty(idDisp) || idDisp == "")
                idDisp = "0";

            string query = "INSERT INTO KEY_MODELO_DISPOSITIVO (MOD_ZONA_ID,MOD_IDENTIFICADOR_USUARIO,MOD_HUELLA,MOD_CARA,MOD_PASS,MOD_ADMIN,INSTANCIA,MOD_DISPOSITIVO) VALUES(";
            query += string.Format("{0}, '{1}', {2}, {3}, {4}, 0, '{5}', {6})",zona,dni,huella,cara,pass,instancia,idDisp);
            logger.LogInformation("addDniPreEnrol: QUERY: " + query);            
            try
            {
                using (SqlCommand command = new SqlCommand(query, connection)) { command.ExecuteNonQuery(); }
            }
            catch (Exception ex)
            {
                 logger.LogError("addKeyModelD: ERROR EN LA QUERY: " + ex.Message);
                return CODIGO_ERROR;
            }
            return CODIGO_EXITO;

        }
        public static int checkModelo(SqlConnection connection, ILogger logger, string dni, string zona, string instancia, string idDisp)
        {
            string addZona, addIdDisp;
            if (zona == "0")            
                addZona = "";            
            else            
                addZona = string.Format("AND MOD_ZONA_ID = {0} ", zona);            

            if (idDisp == "0")             
                addIdDisp = "";            
            else            
                addIdDisp = string.Format("AND MOD_DISPOSITIVO = {0} ",idDisp);

            string query = string.Format("SELECT MOD_ID FROM KEY_MODELO_DISPOSITIVO WHERE MOD_IDENTIFICADOR_USUARIO = '{0}' AND INSTANCIA = '{1}' {2} {3}",dni,instancia,addZona,addIdDisp);
            
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                SqlDataReader response;
                response = command.ExecuteReader();
                try
                {
                    if (response.HasRows)
                    {
                        response.Close();
                        return CODIGO_EXITO;
                    }
                    else
                    {
                        response.Close();
                        return CODIGO_SIN_DATOS;
                    }
                }
                catch (Exception ex)
                {
                    response.Close();
                    logger.LogError("getPlantilla: ERROR EN LA QUERY: " + ex.Message);
                    return CODIGO_ERROR;
                }
            }
        }
        public static int createOrUpdateTemplate(SqlConnection connection, ILogger logger, string dni, string biometria, string indice, int tipoBiometria, string largo, int tipoDisp, int responseGetPlantilla)
        {
            if (tipoBiometria == 1 || tipoBiometria == 2)
            {
                int biometriaSql;
                if (tipoBiometria == 1)
                    biometriaSql = 1;
                else
                {
                    if (tipoDisp == 3)
                        biometriaSql = 3;
                    else
                        biometriaSql = 2;
                }

                try
                {
                    if (responseGetPlantilla == CODIGO_SIN_DATOS)
                    {
                        if (isUpdate)
                        {
                            string query = string.Format("UPDATE KEY_TEMPLATE SET TEM_DATO = '{0}', TEM_LARGO = {1} WHERE TEM_DNI = {2} AND TEM_TIPO = {3}  AND TEM_INDICE = {4}", biometria, largo, dni, biometriaSql, indice);

                            try
                            {
                                using (SqlCommand command1 = new SqlCommand(query, connection)) { command1.ExecuteNonQuery(); }
                            }
                            catch (Exception ex)
                            {
                                logger.LogError("createOrUpdateTemplate - ERROR EN UPDATE: " + ex.Message);
                                return CODIGO_ERROR;
                            }
                        }
                        else //$dni.",".$biometriaSql.",'".$biometria."',".$largo.",".$indice."
                        {

                            string query = string.Format("INSERT INTO KEY_TEMPLATE (TEM_DNI,TEM_TIPO,TEM_DATO,TEM_LARGO,TEM_INDICE) VALUES ({0},{1},'{2}',{3},{4})", dni, biometriaSql, biometria, largo, indice);

                            try
                            {
                                using (SqlCommand command1 = new SqlCommand(query, connection)) { command1.ExecuteNonQuery(); }
                            }
                            catch (Exception ex)
                            {
                                logger.LogError("createOrUpdateTemplate - ERROR EN INSERT: " + ex.Message);
                                return CODIGO_ERROR;
                            }
                        }
                    }
                    else
                    {
                        string query1 = string.Format("SELECT 1 FROM KEY_TEMPLATE WHERE TEM_DATO = '{0}'", biometria);
                        using (SqlCommand command = new SqlCommand(query1, connection))
                        {
                            SqlDataReader response1;
                            response1 = command.ExecuteReader();
                            if (!response1.HasRows)
                            {
                                response1.Close();
                                if (isUpdate)
                                {
                                    string query = string.Format("UPDATE KEY_TEMPLATE SET TEM_DATO = '{0}', TEM_LARGO = {1} WHERE TEM_DNI = {2} AND TEM_TIPO = {3}  AND TEM_INDICE = {4}", biometria, largo, dni, biometriaSql, indice);  
                                    try
                                    {
                                        using (SqlCommand command1 = new SqlCommand(query, connection)) { command1.ExecuteNonQuery(); }
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.LogError("createOrUpdateTemplate - ERROR EN UPDATE: " + ex.Message);
                                        response1.Close();
                                        return CODIGO_ERROR;
                                    }
                                }
                                else //$dni.",".$biometriaSql.",'".$biometria."',".$largo.",".$indice."
                                {
                                    string query = string.Format("INSERT INTO KEY_TEMPLATE (TEM_DNI,TEM_TIPO,TEM_DATO,TEM_LARGO,TEM_INDICE) VALUES ({0},{1},'{2}',{3},{4})", dni, biometriaSql, biometria, largo, indice); // seleccionar todos los bagnumer sucursal 
                                    try
                                    {
                                        using (SqlCommand command1 = new SqlCommand(query, connection)) { command1.ExecuteNonQuery(); }
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.LogError("createOrUpdateTemplate - ERROR EN INSERT: " + ex.Message);
                                        response1.Close();
                                        return CODIGO_ERROR;
                                    }
                                }
                            }
                        }
                    }                        
                }
                catch (Exception ex)
                {
                    logger.LogError("createOrUpdateTemplate - ERROR EN UPDATE: " + ex.Message);
                    return CODIGO_ERROR;
                }
                return CODIGO_EXITO;
            }
            logger.LogInformation("createOrUpdateTemplate - ERROR EN EL TIPO DE BIOMETRIA: " + tipoBiometria.ToString());
            return CODIGO_ERROR;
        }
        public static int getPlantilla(SqlConnection connection, ILogger logger, string dni, string indice, int tipoBiometria, int tipoDisp)
        {
            if (tipoBiometria == 1 || tipoBiometria == 2)
            {
                int biometriaSql;
                if (tipoBiometria == 1)
                    biometriaSql = 1;
                else
                {
                    if (tipoDisp == 3)
                        biometriaSql = 3;
                    else
                        biometriaSql = 2;
                }

                string query = string.Format("SELECT TOP 1 TEM_DATO AS biometria,TEM_INDICE AS indice FROM KEY_TEMPLATE WHERE TEM_DNI ='{0}'  AND TEM_TIPO = {1} AND TEM_INDICE = {2}", dni, biometriaSql, indice); // seleccionar todos los bagnumer sucursal 
                logger.LogInformation("getPlantilla: QUERY: " + query);
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    response = command.ExecuteReader();
                    try
                    {
                        if (!response.HasRows)
                        {
                            response.Close();
                            return CODIGO_SIN_DATOS;
                        }

                        response.Read();                        
                        biometriaGetPlantilla = response[0].ToString();
                        indiceGetPlantilla = response[1].ToString();
                        response.Close();
                        return CODIGO_EXITO;                                                
                    }
                    catch (Exception ex)
                    {
                        response.Close();
                         logger.LogError("getPlantilla: ERROR EN LA QUERY: " + ex.Message);
                        return CODIGO_ERROR;
                    }
                }
            }
            logger.LogInformation("getPlantilla: ERROR EN EL TIPO DE BIOMETRIA: " + tipoBiometria.ToString());
            return CODIGO_ERROR;
        }
        public static int addDniPreEnrol(SqlConnection connection, ILogger logger, string dni, string sn, string pass, string sucursal)
        {
            string query = string.Format("INSERT INTO PRE_ENROLAMIENTO (DNI,SN,CLAVE,ID_SUCURSAL) VALUES('{0}','{1}','{2}',{3})", dni, sn, pass, sucursal); // seleccionar todos los bagnumer sucursal 
             logger.LogInformation("addDniPreEnrol: QUERY: " + query);
            try
            {
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                 logger.LogError("addDniPreEnrol: ERROR EN LA QUERY: " + ex.Message);
                return CODIGO_ERROR;
            }
            return CODIGO_EXITO;

        }
        public static int lookForPreEnrol(SqlConnection connection, ILogger logger, string dni, string sucursal)
        {
            string query = string.Format("SELECT DNI FROM PRE_ENROLAMIENTO WHERE DNI = '{0}' AND ID_SUCURSAL = {1}", dni, sucursal); // seleccionar todos los bagnumer sucursal 
            logger.LogInformation("lookForPreEnrol - QUERY: " + query);
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                response = command.ExecuteReader();
                try
                {
                    if (response.HasRows)
                    {
                        response.Close();
                        return CODIGO_EXITO;
                    }
                    else
                    {
                        response.Close();
                        return CODIGO_SIN_DATOS;
                    }
                }
                catch (Exception ex)
                {
                    response.Close();
                     logger.LogError("lookForPreEnrol - ERROR EN LA QUERY: " + ex.Message);
                    return CODIGO_ERROR;
                }
            }
        }
        public static int findColab(SqlConnection connection, ILogger logger, string dni, string instancia)
        {
            string query = "SELECT Badgenumber AS dni, Name AS nom,privilege AS pZk, CardNo AS tarjeta,  CASE WHEN u.ID_RAZONSOCIAL IS NULL THEN e.EMP_RUT ELSE rs.RAZ_RUT END AS rut_emp,";
            query += " CASE WHEN u.ID_RAZONSOCIAL IS NULL THEN e.EMP_RAZONSOCIAL ELSE rs.RAZ_RAZONSOCIAL END AS nom_emp, CASE WHEN u.ID_RAZONSOCIAL IS NULL THEN e.EMP_DIRECCION ELSE rs.RAZ_DIRECCION END AS dir_emp";
            query += string.Format(" FROM USERINFO u INNER JOIN KEY_EMPRESAS e ON e.EMP_ENTORNO = u.INSTANCIA LEFT JOIN KEY_RAZONSOCIAL rs ON rs.RAZ_ID = u.ID_RAZONSOCIAL WHERE u.Badgenumber = '{0}' AND", dni);
            query += string.Format(" u.INSTANCIA = '{0}' AND HABILITADO > -1 AND TIPO_CUENTA > 1", instancia);

            logger.LogInformation("findColab: QUERY: " + query);
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                response = command.ExecuteReader();
                try
                {
                    if (!response.HasRows)
                    {
                        response.Close();
                        return CODIGO_SIN_DATOS;
                    }
                    response.Read();
                    responseFindColab.Add("dni", response[0].ToString());
                    responseFindColab.Add("nom", response[1].ToString());
                    responseFindColab.Add("pZk", response[2].ToString());
                    responseFindColab.Add("tarjeta", response[3].ToString());
                    responseFindColab.Add("rut_emp", response[4].ToString());
                    responseFindColab.Add("nom_emp", response[5].ToString());
                    responseFindColab.Add("dir_emp", response[6].ToString());

                    response.Close();
                }
                catch (Exception ex)
                {                   
                    response.Close();
                    logger.LogError("findColab - ERROR EN LA QUERY: " + ex.Message);
                    return CODIGO_ERROR;
                }
            }
            return CODIGO_EXITO;
        }
        public static int getInfoDisp(SqlConnection connection, ILogger logger, string sn)
        {
            string query = string.Format("SELECT ID, INSTANCIA, ID_SUCURSAL, TIPO, DIS_TIPO, MachineAlias, LONGITUD, LATITUD,ZONA_ID,ID FROM MACHINES WHERE sn ='{0}'", sn); // seleccionar todos los bagnumer sucursal 
            Dictionary<string, string> responseQuery = new Dictionary<string, string>();

            using (SqlCommand command = new SqlCommand(query, connection))
            {
                logger.LogInformation("getInfoDisp - QUERY: " + query);
                response = command.ExecuteReader();
                try
                {
                    if (response.HasRows)
                    {
                        response.Read();                        
                        responseGetInfoDisp.Add("id", response[0].ToString());
                        responseGetInfoDisp.Add("instancia", response[1].ToString());
                        responseGetInfoDisp.Add("sucursal", response[2].ToString());
                        responseGetInfoDisp.Add("tipo", response[3].ToString());
                        responseGetInfoDisp.Add("dis_tipo", response[4].ToString());
                        responseGetInfoDisp.Add("alias", response[5].ToString());
                        responseGetInfoDisp.Add("longitud", response[6].ToString());
                        responseGetInfoDisp.Add("latitud", response[7].ToString());
                        responseGetInfoDisp.Add("zona", response[8].ToString());
                        
                    }
                    else
                        return CODIGO_SIN_DATOS;
                    
                    response.Close();
                }
                catch (Exception ex)
                {
                    response.Close();
                    logger.LogError("getInfoDisp - ERROR EN LA QUERY: " + ex.Message);
                    return CODIGO_ERROR;
                }
                return CODIGO_EXITO;
            }
        }
        public static void getDispsModelo(SqlConnection connection, ILogger logger, string dni, string idDisp,string instancia,string sucursal) 
        {
            string query = "SELECT d.sn FROM KEY_MODELO_DISPOSITIVO m INNER JOIN MACHINES d ON d.ID = m.MOD_DISPOSITIVO";
            query += string.Format(" WHERE m.MOD_IDENTIFICADOR_USUARIO = '{0}'  AND d.ID <> {1}  AND m.INSTANCIA = '{2}' AND d.ID_SUCURSAL = {3}",dni,idDisp,instancia,sucursal);
                        
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                 logger.LogInformation("QUERY PARA SELECCIONAR LOS COLABORADORES: " + query);
                response = command.ExecuteReader();
                try
                {
                    if (response.HasRows) 
                    {
                        while (response.Read())                        
                            listSN.Add(response[0].ToString());//response[0] userinfo de cada uno insertar la transaccion                         
                    }                    
                    response.Close();
                }
                catch (Exception ex)
                {
                     logger.LogError("getDispsModelo - ERROR EN LA QUERY: " + ex.Message);
                    response.Close();
                }
            }            
        }

       /* public static string checkHashModel(ILogger logger, string sn, string dni, string nomColab, string rutEmp, string nomEmp, string dirEmp, string pZk, string tarjeta, string grupo, string biometria, string indice, string largo, string tipoBiometria, string pass, snZk) 
        {
            string jsonStrBiophoto = "";
            string jsonStrFinger = "";
            string jsonStrUserInfo = "";

            try
            {
                jsonUserInfo = new JsonUserInfo();

                jsonUserInfo.dni = dni;
                jsonUserInfo.nombre = nomColab;
                jsonUserInfo.rut_emp = rutEmp;
                jsonUserInfo.nombre_emp = nomEmp;
                jsonUserInfo.direccion_emp = dirEmp;
                jsonUserInfo.privilegio = pZk;
                jsonUserInfo.tarjeta = tarjeta;
                jsonUserInfo.grupo = grupo;
                jsonUserInfo.pass = pass;

                jsonStrUserInfo = JsonConvert.SerializeObject(jsonUserInfo);

                if (tipoBiometria == "1")
                {
                    jsonFinger = new JsonFinger();
                    jsonFinger.dni = dni;
                    jsonFinger.huella = biometria;
                    jsonFinger.indice_dedo = indice;
                    jsonFinger.largo_huella = largo;
                    jsonStrFinger = JsonConvert.SerializeObject(jsonFinger);

                }
                else if (tipoBiometria == "2")
                {
                    string biometriaReducida = "";
                    try
                    {
                        if (biometria.Length > imgSize)
                            biometriaReducida = ImgProcess.reducctionQuality(biometria);
                        else
                            biometriaReducida = biometria;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("ERROR AL REDUCIR EL TAMANHO DE LA BIOMETRIA: " + ex.Message);

                    }

                    if (biometriaReducida == "-1")
                    {
                        logger.LogError("ERROR AL REDUCIR EL TAMANHO DE LA BIOMETRIA: " + biometria);
                        return "";
                    }
                    else
                    {
                        jsonBiophoto = new JsonBiophoto();
                        jsonBiophoto.dni = dni;
                        jsonBiophoto.cara = biometriaReducida;
                        jsonBiophoto.indice_cara = indice;
                        jsonBiophoto.largo_cara = largo;
                        jsonStrBiophoto = JsonConvert.SerializeObject(jsonBiophoto);
                    }
                }
                else
                {
                    logger.LogError("EL TIPO DE BIOMETRIA NO ESTA PERMITIDO. TIPO BIOMETRIA: " + tipoBiometria);
                    return "";
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex.Message);
                return "";
            }
            using (SqlConnection connection = new SqlConnection(SQL_CONNECTION_TRANSACTIONS))
            {
                try
                {
                    connection.Open();
                    
                    string query = "INSERT INTO TRANSACCIONES (TRA_TIPO,TRA_ESTADO,TRA_DETALLE,TRA_SN,TRA_MENSAJE,TRA_HORA_INICIO,TRA_DATA_1,TRA_ORIGEN) VALUES(6, 0, ";
                    query += string.Format("'{0}', '{1}', 'UP_USERINFO', '{2}', '{3}', 'template-addPublish')", jsonStrUserInfo, snZk, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), sn);

                    string logUser = string.Format("'{0}', '{1}', 'UP_USERINFO', '{2}', '{3}', 'template-addPublish')", string.Format("DNI: {0} NAME_EMP: {1}, PRIV: {2}, PASS: {3}", dni, nomEmp, pZk, pass), snZk, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), sn);

                    logger.LogInformation("addPublish: QUERY-UP_USERINFO: " + logUser);
                    try
                    {
                        using (SqlCommand command = new SqlCommand(query, connection))
                        {
                            command.ExecuteNonQuery();
                        }
                        if (tipoBiometria == "1")
                        {
                            query = "INSERT INTO TRANSACCIONES (TRA_TIPO,TRA_ESTADO,TRA_DETALLE,TRA_SN,TRA_MENSAJE,TRA_HORA_INICIO,TRA_DATA_1,TRA_ORIGEN) VALUES(7, 0, ";
                            string log = query;
                            query += string.Format("'{0}', '{1}', 'UP_FINGERTEMP', '{2}', '{3}', 'template-addPublish')", jsonStrFinger, snZk, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), sn);
                            log += string.Format("'{0}', '{1}', 'UP_FINGERTEMP', '{2}', '{3}', 'template-addPublish')", string.Format("DNI: {0} INDICE: {1}, LARGO: {2}", dni, indice, largo), snZk, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), sn);
                            logger.LogInformation("addPublish: QUERY-UP_FINGERTEMP: " + log);

                            using (SqlCommand command = new SqlCommand(query, connection)) { command.ExecuteNonQuery(); }

                            //CODIGO 1
                        }
                        else if (tipoBiometria == "2")
                        {
                            query = "INSERT INTO TRANSACCIONES (TRA_TIPO,TRA_ESTADO,TRA_DETALLE,TRA_SN,TRA_MENSAJE,TRA_HORA_INICIO,TRA_DATA_1,TRA_ORIGEN) VALUES(8, 0, ";
                            string log = query;
                            query += string.Format("'{0}', '{1}', 'UP_FACETEMP', '{2}', '{3}', 'template-addPublish')", jsonStrBiophoto, snZk, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), sn);
                            log += string.Format("'{0}', '{1}', 'UP_FACETEMP', '{2}', '{3}', 'template-addPublish')", string.Format("DNI: {0} INDICE: {1}, LARGO: {2}", dni, indice, largo), snZk, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), sn);
                            logger.LogInformation("addPublish: QUERY-UP_FACETEMP: " + log);

                            using (SqlCommand command = new SqlCommand(query, connection)) { command.ExecuteNonQuery(); }

                            //CODIGO 2                                
                        }
                        else
                        {
                            logger.LogError("addDniPreEnrol: ERROR EN LA QUERY: ");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogError("addDniPreEnrol: ERROR EN LA QUERY: " + ex.Message);
                        //return CODIGO_ERROR;
                    }
                    
                    //ACA IBA EL CODIGO 3 

                    connection.Close();
                }
                catch (Exception ex)
                {
                    logger.LogError("ERROR: " + ex.Message);
                    connection.Close();
                }
            }
            return "";
        }*/
        public static void addPublish( ILogger logger, string sn,string dni, string nomColab, string rutEmp, string nomEmp ,string dirEmp, string pZk, string tarjeta, string grupo, string biometria, string indice, string largo, string tipoBiometria, string pass,List<string> dispsModelo) 
        {
            string jsonStrBiophoto = "";
            string jsonStrFinger = "";
            string jsonStrUserInfo = "";

            try
            {
                jsonUserInfo = new JsonUserInfo();

                jsonUserInfo.dni = dni;
                jsonUserInfo.nombre = nomColab;
                jsonUserInfo.rut_emp = rutEmp;
                jsonUserInfo.nombre_emp = nomEmp;
                jsonUserInfo.direccion_emp = dirEmp;
                jsonUserInfo.privilegio = pZk;
                jsonUserInfo.tarjeta = tarjeta;
                jsonUserInfo.grupo = grupo;
                jsonUserInfo.pass = pass;

                jsonStrUserInfo = JsonConvert.SerializeObject(jsonUserInfo);

                if (tipoBiometria == "1")
                {
                    jsonFinger = new JsonFinger();
                    jsonFinger.dni = dni;
                    jsonFinger.huella = biometria;
                    jsonFinger.indice_dedo = indice;
                    jsonFinger.largo_huella = largo;
                    jsonStrFinger = JsonConvert.SerializeObject(jsonFinger);

                }
                else if (tipoBiometria == "2")
                {
                    string biometriaReducida = "";
                    try 
                    {
                        if (biometria.Length > imgSize)
                            biometriaReducida = ImgProcess.reducctionQuality(biometria);
                        else
                            biometriaReducida = biometria;
                    } 
                    catch (Exception ex)
                    {
                        logger.LogError("ERROR AL REDUCIR EL TAMANHO DE LA BIOMETRIA: " + ex.Message);

                    }

                    if (biometriaReducida == "-1")
                    {
                        logger.LogError("ERROR AL REDUCIR EL TAMANHO DE LA BIOMETRIA: " + biometria);
                        return;
                    }
                    else
                    {
                        jsonBiophoto = new JsonBiophoto();
                        jsonBiophoto.dni = dni;
                        jsonBiophoto.cara = biometriaReducida;
                        jsonBiophoto.indice_cara = indice;
                        jsonBiophoto.largo_cara = largo;
                        jsonStrBiophoto = JsonConvert.SerializeObject(jsonBiophoto);
                    }
                }
                else
                {
                    logger.LogError("EL TIPO DE BIOMETRIA NO ESTA PERMITIDO. TIPO BIOMETRIA: " + tipoBiometria);
                    return;
                }
            }
            catch (Exception ex) 
            {
                logger.LogError(ex.Message);
                return;
            }
            using (SqlConnection connection = new SqlConnection(SQL_CONNECTION_TRANSACTIONS))
            {
                try
                {
                    connection.Open();
                    foreach (string snZk in dispsModelo)
                    {
                        string query = "INSERT INTO TRANSACCIONES (TRA_TIPO,TRA_ESTADO,TRA_DETALLE,TRA_SN,TRA_MENSAJE,TRA_HORA_INICIO,TRA_DATA_1,TRA_ORIGEN) VALUES(6, 0, ";
                        query += string.Format("'{0}', '{1}', 'UP_USERINFO', '{2}', '{3}', 'template-addPublish')", jsonStrUserInfo, snZk, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), sn);

                        string logUser = string.Format("'{0}', '{1}', 'UP_USERINFO', '{2}', '{3}', 'template-addPublish')", string.Format("DNI: {0} NAME_EMP: {1}, PRIV: {2}, PASS: {3}", dni, nomEmp, pZk,pass), snZk, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), sn);

                        logger.LogInformation("addPublish: QUERY-UP_USERINFO: " + logUser);
                        try
                        {
                            using (SqlCommand command = new SqlCommand(query, connection))
                            {
                                command.ExecuteNonQuery();
                            }
                            if (tipoBiometria == "1") 
                            {    
                                query = "INSERT INTO TRANSACCIONES (TRA_TIPO,TRA_ESTADO,TRA_DETALLE,TRA_SN,TRA_MENSAJE,TRA_HORA_INICIO,TRA_DATA_1,TRA_ORIGEN) VALUES(7, 0, ";
                                string log = query;
                                query += string.Format("'{0}', '{1}', 'UP_FINGERTEMP', '{2}', '{3}', 'template-addPublish')", jsonStrFinger, snZk, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), sn);
                                log += string.Format("'{0}', '{1}', 'UP_FINGERTEMP', '{2}', '{3}', 'template-addPublish')", string.Format("DNI: {0} INDICE: {1}, LARGO: {2}",dni,indice,largo), snZk, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), sn);
                                logger.LogInformation("addPublish: QUERY-UP_FINGERTEMP: " + log);

                                using (SqlCommand command = new SqlCommand(query, connection)) { command.ExecuteNonQuery(); }                                

                                //CODIGO 1
                            }
                            else if (tipoBiometria == "2") 
                            {                                
                                query = "INSERT INTO TRANSACCIONES (TRA_TIPO,TRA_ESTADO,TRA_DETALLE,TRA_SN,TRA_MENSAJE,TRA_HORA_INICIO,TRA_DATA_1,TRA_ORIGEN) VALUES(8, 0, ";
                                string log = query;
                                query += string.Format("'{0}', '{1}', 'UP_FACETEMP', '{2}', '{3}', 'template-addPublish')", jsonStrBiophoto, snZk, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), sn);
                                log += string.Format("'{0}', '{1}', 'UP_FACETEMP', '{2}', '{3}', 'template-addPublish')", string.Format("DNI: {0} INDICE: {1}, LARGO: {2}", dni, indice, largo), snZk, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), sn);
                                logger.LogInformation("addPublish: QUERY-UP_FACETEMP: " + log);

                                using (SqlCommand command = new SqlCommand(query, connection)){ command.ExecuteNonQuery(); }                             

                                //CODIGO 2                                
                            }
                            else
                            {
                                 logger.LogError("addDniPreEnrol: ERROR EN LA QUERY: ");
                            }
                        }
                        catch (Exception ex)
                        {
                             logger.LogError("addDniPreEnrol: ERROR EN LA QUERY: " + ex.Message);
                            //return CODIGO_ERROR;
                        }
                    }
                    //ACA IBA EL CODIGO 3 

                    connection.Close();
                }
                catch (Exception ex)
                {
                     logger.LogError("ERROR: " + ex.Message);
                    connection.Close();
                }
            }   
        }
        #endregion
    }
}