using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace KinectfaceProject
{
    class DBConnection
    {


        public static SqlConnection getConnection()
        {
            string connetionString = null;
            SqlConnection cnn ;
            connetionString = @"Data Source=(LocalDB)\v11.0;AttachDbFilename=C:\Users\VJagwani\Documents\MS_Kinect\KinectfaceProject\KinectfaceProject\employeeDB.mdf;Integrated Security=True;";
            cnn = new SqlConnection(connetionString);
            return cnn;
        }
    }
}
