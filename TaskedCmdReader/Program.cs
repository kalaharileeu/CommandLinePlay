using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.IO;

using System.Runtime.InteropServices;

using System.Diagnostics;

namespace TaskedCmdReader
{
    class Program
    {
        static string readvalue(ref System.IntPtr ptr, short a)
        {
            SMALL_RECT srctReadRect = new SMALL_RECT
            {
                Top = 0,
                Left = 0,
                Bottom = 1,
                Right = 80
            };
            CHAR_INFO[,] chiBuffer = new CHAR_INFO[2, 80]; // [2][80];10 lines,  with 50 characters

            COORD coordBufSize = new COORD
            {
                X = 80,
                Y = 2
            };
            COORD coordBufCoord = new COORD
            {
                X = 0,
                Y = 0
            };

            bool fSuccess;
            int i = 0;
            int j = 0;
            string chartostring = "start";
            string previousstring = "";

            short g = a;
            short h = (short)(g + 1);

            srctReadRect.Top = g;
            srctReadRect.Bottom = h;
            int count = 0;

            //System.Console.WriteLine(g + "." + h);
            while (count < 1)//Hunting:if it find 1 empty rows with text then it will stop reading
            {
                previousstring = chartostring;
                srctReadRect.Top = g;
                srctReadRect.Bottom = h;

                fSuccess = ReadConsoleOutput(ptr, chiBuffer, coordBufSize, coordBufCoord, ref srctReadRect);

                i = 0;
                j = 0;
                chartostring = "";
                while (j < coordBufSize.Y)
                {
                    while (i < coordBufSize.X)
                    {
                        if (chiBuffer[j, i].UnicodeChar != 0 && chiBuffer[j, i].UnicodeChar != 32)
                            chartostring += chiBuffer[j, i].UnicodeChar;
                        i++;
                    }
                    i = 0;
                    j++;
                }

                if (chartostring.Length == 0)//The character length is zero, reverse the top of the source rect
                {
                    count++;
                }
                else
                {
                    count = 0;
                }
                g += 1;
                h += 1;
            }
            return previousstring;
        }

        public struct COORD
        {
            public short X;
            public short Y;
        }
        //CHAR_INFO struct, which was a union in the old days
        // so we want to use LayoutKind.Explicit to mimic it as closely
        // as we can
        [StructLayout(LayoutKind.Explicit)]
        public struct CHAR_INFO
        {
            [FieldOffset(0)]
            public char UnicodeChar;
            [FieldOffset(0)]
            public char AsciiChar;
            [FieldOffset(2)] //2 bytes seems to work properly
            UInt16 Attributes;

            public char value()
            {
                return UnicodeChar;
            }
        }

        public struct SMALL_RECT
        {
            public short Left;
            public short Top;
            public short Right;
            public short Bottom;
        }

        // http://pinvoke.net/default.aspx/kernel32/ReadConsoleOutput.html
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool ReadConsoleOutput(
            IntPtr hConsoleOutput,
            [Out] CHAR_INFO[,] lpBuffer,
            COORD dwBufferSize,
            COORD dwBufferCoord,
            ref SMALL_RECT lpReadRegion
            );

        // http://pinvoke.net/default.aspx/kernel32/AttachConsole.html
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool AttachConsole(
            uint dwProcessId
            );

        // http://pinvoke.net/default.aspx/kernel32/GetStdHandle.html
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool FreeConsole();

        static public string reporttoserver(ref string status, ref string error, HttpWebRequest req, HttpWebResponse resp)
        {
            string responsestring = ".";
            //HttpWebRequest req = null;
            //HttpWebResponse resp = null;
            try
            {
                req = (HttpWebRequest)WebRequest.
                    Create("http://nz-hwlab-ws1:8000/dashboard/update/?status=" + status + "&script=cornelbench"); //Complete
            }
            catch (System.Net.WebException e)
            {
                error = e.Message;
                //System.Console.WriteLine(error);
                return "ServerError";
            }
            try
            {
                resp = (HttpWebResponse)req.GetResponse();
            }
            catch (System.Net.WebException e)
            {
                error = e.Message;
                ///System.Console.WriteLine(error);
                return "ServerError";
            }
            // From the response, obtain an input stream.
            Stream istrm = resp.GetResponseStream();
            int ch;
            for (int ij = 1; ; ij++)
            {
                ch = istrm.ReadByte();
                if (ch == -1) break;
                responsestring += ((char)ch);
            }
            resp.Close();


            return responsestring;
        }
        //this method runs asynchronously
        static async void Runmainprocess()
        {
            string t = await Task.Run(() => MainProcess());
            System.Console.Write(".");
            Runmainprocess();
        }

        static string MainProcess()//This method is used by the asynchronous method
        {
            string status = ".";
            List<Process> listprocess = new List<Process>();
            //var poList= Process.GetProcesses().Where(process => process.ProcessName.Contains("cmd"));
            foreach (Process p in Process.GetProcesses())
            {
                if (p.ProcessName.Equals("cmd"))
                    listprocess.Add(p);
            }

            if (listprocess.Count() == 0)//if there are no processes running then get out
            {
                System.Diagnostics.Process.Start("C:\\Windows\\System32\\Cmd.exe");
                Thread.Sleep(1000);
                foreach (Process p in Process.GetProcesses())
                {
                    if (p.ProcessName.Equals("cmd"))
                        listprocess.Add(p);
                }
            }

            bool processloop = true;
            while (processloop)
            {
                foreach (Process p in listprocess)
                {
                    System.Console.ForegroundColor = ConsoleColor.Cyan;
                    var a = p;
                    FreeConsole();//if you use console application you must free self console
                    bool v = AttachConsole((uint)a.Id);
                    var err = Marshal.GetLastWin32Error();
                    //System.Console.WriteLine(err);
                    System.IntPtr ptr = GetStdHandle(-11);
                    bool imrunning = true;
                    while (imrunning && v)
                    {
                        //FreeConsole();
                        short cursor = (short)System.Console.CursorTop;//Find the bottomline where cursor resides
                        string checkforcursor = readvalue(ref ptr, cursor);
                        if (checkforcursor.StartsWith("C:\\>"))
                        {
                            status = "Complete";
                            imrunning = false;
                            //reporttoserver(ref status, ref error, req, resp);
                        }
                        else if (checkforcursor.StartsWith("C:\\"))
                        {
                            status = "Complete";
                            imrunning = false;
                            //reporttoserver(ref status, ref error, req, resp);
                        }
                        else
                        {
                            System.Console.ForegroundColor = ConsoleColor.Red;
                            status = "Running";
                            imrunning = true;
                            System.Console.Write("'");
                            //reporttoserver(ref status, ref error, req, resp);

                        }
                        Thread.Sleep(300);//2000
                    }
                    System.Console.ForegroundColor = ConsoleColor.White;
                    Thread.Sleep(300);//10 000One process closed move to the next
                }

                processloop = false;//No processes
            }
            return status;
        }

        static void Main()
        {
            System.Diagnostics.Process.Start("C:\\Windows\\System32\\Cmd.exe");

            Process p = new Process(); // create process (i.e., the python program
            p.StartInfo.FileName = "python.exe";
            p.StartInfo.RedirectStandardOutput = false;
            p.StartInfo.UseShellExecute = true; // make sure we can read the output from stdout
            p.StartInfo.Arguments = "c:\\test\\testtime.py "; // start the python program with two parameters
            p.Start(); // start the process (the python program)
            p.WaitForExit();
            if (p.HasExited)
                p.Close();
/*            StreamReader s = p.StandardOutput;
            String output = s.ReadToEnd();
            string[] r = output.Split(new char[] { ' ' }); // get the parameter
            Console.WriteLine(r[0]);
*/
            /*string status = "unknown";
            string error = ".";
            HttpWebRequest req = null;
            HttpWebResponse resp = null;*/
            Runmainprocess();

            while (true)
            {
                //System.Diagnostics.Process.Start("C:\\Windows\\System32\\Cmd.exe");
                //Runmainprocess();//Starts computation
                //Handle user in put
                //string ouput = MainProcess();
                //Console.Write(".");
               // string jj = Console.ReadLine();
               // Console.WriteLine("You typed: " );

                Thread.Sleep(300);
            }
        }
    }
}
//Tutorial
/*
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics; // Process
using System.IO; // StreamWriter
namespace ConsoleApplication1
{
    class Program
    {
        static void Main(string[] args)
        {
            // the python program as a string. Note '@' which allow us to have a multiline string
            String prg = @"import sys
                x = int(sys.argv[1])
                y = int(sys.argv[2])
                print x+y";
 * 
            StreamWriter sw = new StreamWriter("c:\\kudos\\test2.py");
            sw.Write(prg); // write this program to a file
            sw.Close();
 
            int a = 2;
            int b = 2;
 
            Process p = new Process(); // create process (i.e., the python program
            p.StartInfo.FileName = "python.exe";
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.UseShellExecute = false; // make sure we can read the output from stdout
            p.StartInfo.Arguments = "c:\\kudos\\test2.py "+a+" "+b; // start the python program with two parameters
            p.Start(); // start the process (the python program)
            StreamReader s = p.StandardOutput; 
            String output = s.ReadToEnd();
            string []r = output.Split(new char[]{' '}); // get the parameter
            Console.WriteLine(r[0]);
            p.WaitForExit();
            Console.ReadLine(); // wait for a key press
        }
    }
}
 
 */

