using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
namespace CloudMusicRemote
{
    public static class Music
    {
        static readonly object gate=new object();
        public static string Title()
        {
            foreach(var p in Process.GetProcessesByName("cloudmusic"))using(p)
            {
                if(!String.IsNullOrEmpty(p.MainWindowTitle))return p.MainWindowTitle;
            }
            return "网易云音乐";
        }
        public static bool Running()
        {
            var ps=Process.GetProcessesByName("cloudmusic");
            bool found=ps.Length>0;
            foreach(var p in ps)p.Dispose();
            return found;
        }
        public static void Control(string action)
        {
            Shortcut binding=Preferences.Get(action);
            ushort key=(ushort)binding.Key;
            lock(gate)
            {
                if(!Running())throw new InvalidOperationException("请先在电脑上打开网易云音乐。");
                // Wait for user modifiers to be released. Never release a key held by the user.
                var wait=Stopwatch.StartNew();
                while(Held(0x10)||Held(0x11)||Held(0x12)||Held(0x5B)||Held(0x5C))
                {
                    if(wait.ElapsedMilliseconds>1800)throw new InvalidOperationException("请松开 Ctrl / Alt / Shift / Win 键后再试。");
                    Thread.Sleep(20);
                }
                var input=new List<INPUT>();
                var modifiers=new List<ushort>();
                if((binding.Modifiers&1)!=0)modifiers.Add(0x11);
                if((binding.Modifiers&2)!=0)modifiers.Add(0x12);
                if((binding.Modifiers&4)!=0)modifiers.Add(0x10);
                foreach(var modifier in modifiers)input.Add(Key(modifier,false));
                input.Add(Key(key,false)); input.Add(Key(key,true));
                for(int i=modifiers.Count-1;i>=0;i--)input.Add(Key(modifiers[i],true));
                if(SendInput((uint)input.Count,input.ToArray(),Marshal.SizeOf(typeof(INPUT)))!=input.Count)
                {
                    var release=new List<INPUT>();release.Add(Key(key,true));
                    for(int i=modifiers.Count-1;i>=0;i--)release.Add(Key(modifiers[i],true));
                    SendInput((uint)release.Count,release.ToArray(),Marshal.SizeOf(typeof(INPUT)));
                    throw new InvalidOperationException("无法发送快捷键；请让网易云与本工具使用相同权限运行。");
                }
            }
        }
        static bool Held(int key)
        {
            return (GetAsyncKeyState(key)&0x8000)!=0;
        }
        static INPUT Key(ushort key,bool up)
        {
            return new INPUT
            {
                type=1,U=new InputUnion
                {
                    ki=new KEYBDINPUT
                    {
                        wVk=key,dwFlags=(up?2u:0u)|((key>=0x25&&key<=0x28)?1u:0u)
                    }
                }
            };
        }
        [StructLayout(LayoutKind.Sequential)]struct INPUT
        {
            public uint type;
            public InputUnion U;
        }
        [StructLayout(LayoutKind.Explicit)]struct InputUnion
        {
            [FieldOffset(0)]public KEYBDINPUT ki;
            [FieldOffset(0)]public MOUSEINPUT mi;
        }
        [StructLayout(LayoutKind.Sequential)]struct KEYBDINPUT
        {
            public ushort wVk,wScan;
            public uint dwFlags,time;
            public UIntPtr dwExtraInfo;
        }
        [StructLayout(LayoutKind.Sequential)]struct MOUSEINPUT
        {
            public int dx,dy;
            public uint mouseData,dwFlags,time;
            public UIntPtr dwExtraInfo;
        }
        [DllImport("user32.dll",SetLastError=true)]static extern uint SendInput(uint count,INPUT[] inputs,int size);
        [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
    }
    public sealed class MouseHook : IDisposable
    {
        public volatile int Button=2;
        public volatile bool Enabled=true;
        IntPtr handle;
        HookProc proc;
        Action next;
        long last;
        int captured;
        delegate IntPtr HookProc(int code,IntPtr message,IntPtr data);
        public MouseHook(Action action)
        {
            next=action;
            proc=Callback;
            handle=SetWindowsHookEx(14,proc,GetModuleHandle(null),0);
            if(handle==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        IntPtr Callback(int code,IntPtr message,IntPtr data)
        {
            if(code>=0)
            {
                int msg=message.ToInt32();
                if(msg==0x20B||msg==0x20C)
                {
                    var m=(MouseData)Marshal.PtrToStructure(data,typeof(MouseData));
                    int b=(int)(m.mouseData>>16);
                    if(msg==0x20C&&captured==b)
                    {
                        captured=0;
                        return (IntPtr)1;
                    }
                    if(Enabled&&b==Button&&msg==0x20B&&(m.flags&1)==0)
                    {
                        captured=b;
                        long now=Stopwatch.GetTimestamp();
                        if(now-last>Stopwatch.Frequency*0.3)
                        {
                            last=now;
                            next();
                        }
                        return (IntPtr)1;
                    }
                }
            }
            return CallNextHookEx(handle,code,message,data);
        }
        public void Dispose()
        {
            if(handle!=IntPtr.Zero)
            {
                UnhookWindowsHookEx(handle);
                handle=IntPtr.Zero;
            }
        }
        [StructLayout(LayoutKind.Sequential)]struct MouseData
        {
            public int x,y;
            public uint mouseData,flags,time;
            public UIntPtr extra;
        }
        [DllImport("user32.dll",SetLastError=true)]static extern IntPtr SetWindowsHookEx(int id,HookProc callback,IntPtr module,uint thread);
        [DllImport("user32.dll")]static extern bool UnhookWindowsHookEx(IntPtr h);
        [DllImport("user32.dll")]static extern IntPtr CallNextHookEx(IntPtr h,int code,IntPtr msg,IntPtr data);
        [DllImport("kernel32.dll",CharSet=CharSet.Auto)]static extern IntPtr GetModuleHandle(string name);
    }
    public sealed class RemoteServer : IDisposable
    {
        public readonly string PairCode;
        public readonly int Port;
        TcpListener listener;
        volatile bool stopping;
        string token;
        byte[] html;
        SemaphoreSlim clients=new SemaphoreSlim(12);
        readonly object authGate=new object();
        DateTime authWindow=DateTime.UtcNow;
        int attempts;
        DateTime lastCommand=DateTime.MinValue;
        public RemoteServer()
        {
            using(var rng=RandomNumberGenerator.Create())
            {
                byte[] b=new byte[24];
                rng.GetBytes(b);
                token=Convert.ToBase64String(b);
                uint n;
                do
                {
                    rng.GetBytes(b);
                    n=BitConverter.ToUInt32(b,0);
                }
                while(n>=4294000000u);
                PairCode=(n%1000000).ToString("D6");
            }
            using(var stream=typeof(RemoteServer).Assembly.GetManifestResourceStream("remote.html"))using(var ms=new MemoryStream())
            {
                stream.CopyTo(ms);
                html=ms.ToArray();
            }
            for(int p=17663;p<17673;p++)
            {
                try
                {
                    listener=new TcpListener(IPAddress.Any,p);
                    listener.Server.ExclusiveAddressUse=true;
                    listener.Start(16);
                    Port=p;
                    return;
                }
                catch(SocketException)
                {
                    if(listener!=null)listener.Stop();
                }
            }
            throw new IOException("17663–17672 端口都被占用，请退出旧工具后重试。");
        }
        public string[] Links()
        {
            var list=new List<string>();
            foreach(var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if(ni.OperationalStatus!=OperationalStatus.Up||ni.NetworkInterfaceType==NetworkInterfaceType.Loopback||ni.NetworkInterfaceType==NetworkInterfaceType.Tunnel)continue;
                var properties=ni.GetIPProperties();
                if(properties.GatewayAddresses.Count==0)continue;
                foreach(var u in properties.UnicastAddresses)
                {
                    var a=u.Address;
                    if(a.AddressFamily==AddressFamily.InterNetwork&&Private(a))list.Add("http://"+a+":"+Port+"/");
                }
            }
            list.Sort((a,b)=>AddressRank(a).CompareTo(AddressRank(b)));
            if(list.Count==0)list.Add("http://127.0.0.1:"+Port+"/");
            return list.ToArray();
        }
        static int AddressRank(string url)
        {
            return url.StartsWith("http://192.168.")?0:url.StartsWith("http://10.")?1:2;
        }
        public void Start()
        {
            var t=new Thread(Accept)
            {
                IsBackground=true,Name="Phone remote server"
            };
            t.Start();
        }
        void Accept()
        {
            while(!stopping)
            {
                try
                {
                    var client=listener.AcceptTcpClient();
                    if(!clients.Wait(0))
                    {
                        client.Close();
                        continue;
                    }
                    ThreadPool.QueueUserWorkItem(_=>
                    {
                        try
                        {
                            Handle(client);
                        }
                        catch
                        {
                        }
                        finally
                        {
                            client.Close();clients.Release();
                        }
                    });
                }
                catch
                {
                    if(!stopping)Thread.Sleep(100);
                }
            }
        }
        public static bool Private(IPAddress a)
        {
            var b=a.GetAddressBytes();
            return b.Length==4&&(b[0]==127||b[0]==10||(b[0]==172&&b[1]>=16&&b[1]<=31)||(b[0]==192&&b[1]==168)||(b[0]==169&&b[1]==254));
        }
        void Handle(TcpClient c)
        {
            c.ReceiveTimeout=3000;
            c.SendTimeout=3000;
            using(var s=c.GetStream())
            {
                if(!Private(((IPEndPoint)c.Client.RemoteEndPoint).Address))
                {
                    Reply(s,403,new
                    {
                        error="仅允许局域网访问"
                    });
                    return;
                }
                var deadline=Stopwatch.StartNew();
                byte[] buf=new byte[8192];
                int n=0;
                bool complete=false;
                while(n<buf.Length)
                {
                    if(deadline.ElapsedMilliseconds>=3000)return;
                    c.ReceiveTimeout=Math.Max(1,3000-(int)deadline.ElapsedMilliseconds);
                    int v=s.ReadByte();
                    if(v<0)return;
                    buf[n++]=(byte)v;
                    if(n>=4&&buf[n-4]==13&&buf[n-3]==10&&buf[n-2]==13&&buf[n-1]==10)
                    {
                        complete=true;
                        break;
                    }
                }
                if(!complete)
                {
                    Reply(s,431,new
                    {
                        error="请求头过长"
                    });
                    return;
                }
                var lines=Encoding.ASCII.GetString(buf,0,n).Split(new[]
                {
                    "\r\n"
                },StringSplitOptions.None);
                var first=lines[0].Split(' ');
                if(first.Length!=3)
                {
                    Reply(s,400,new
                    {
                        error="请求格式错误"
                    });
                    return;
                }
                string method=first[0],path=first[1];
                var headers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                for(int i=1;i<lines.Length;i++)
                {
                    int colon=lines[i].IndexOf(':');
                    if(colon>0)
                    {
                        var name=lines[i].Substring(0,colon);
                        if(headers.ContainsKey(name))
                        {
                            Reply(s,400,new
                            {
                                error="重复请求头"
                            });
                            return;
                        }
                        headers[name]=lines[i].Substring(colon+1).Trim();
                    }
                }
                if(headers.ContainsKey("Transfer-Encoding"))
                {
                    Reply(s,400,new
                    {
                        error="不支持分块请求"
                    });
                    return;
                }
                string rawLength;
                int length=0;
                if(headers.TryGetValue("Content-Length",out rawLength)&&(!Int32.TryParse(rawLength,out length)||length<0||length>1024))
                {
                    Reply(s,400,new
                    {
                        error="无效请求长度"
                    });
                    return;
                }
                var bytes=new byte[length];
                int read=0;
                while(read<length)
                {
                    int got=s.Read(bytes,read,length-read);
                    if(got<=0)return;
                    read+=got;
                }
                string host;
                if(!headers.TryGetValue("Host",out host)||!ValidHost(host))
                {
                    Reply(s,403,new
                    {
                        error="无效主机地址"
                    });
                    return;
                }
                string origin;
                if(headers.TryGetValue("Origin",out origin)&&origin!="http://"+host)
                {
                    Reply(s,403,new
                    {
                        error="拒绝跨站请求"
                    });
                    return;
                }
                if(method=="GET"&&path=="/")
                {
                    Write(s,200,"text/html; charset=utf-8",html);
                    return;
                }
                if(path=="/favicon.ico")
                {
                    Write(s,204,"image/x-icon",new byte[0]);
                    return;
                }
                if(path!="/api/pair")
                {
                    string supplied;
                    if(!headers.TryGetValue("X-Remote-Token",out supplied)||!Equal(supplied,token))
                    {
                        Reply(s,401,new
                        {
                            error="请使用电脑上显示的配对码重新连接"
                        });
                        return;
                    }
                }
                if(method=="GET"&&path=="/api/status")
                {
                    Reply(s,200,new
                    {
                        running=Music.Running(),title=Music.Title()
                    });
                    return;
                }
                if(method!="POST"||(path!="/api/pair"&&path!="/api/control"))
                {
                    Reply(s,404,new
                    {
                        error="未找到操作"
                    });
                    return;
                }
                if(length<1)
                {
                    Reply(s,400,new
                    {
                        error="无效请求长度"
                    });
                    return;
                }
                string contentType;
                if(!headers.TryGetValue("Content-Type",out contentType)||!contentType.StartsWith("application/json",StringComparison.OrdinalIgnoreCase))
                {
                    Reply(s,415,new
                    {
                        error="需要 JSON 请求"
                    });
                    return;
                }
                Dictionary<string,object> body;
                try
                {
                    body=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(bytes));
                    if(body==null)throw new Exception();
                }
                catch
                {
                    Reply(s,400,new
                    {
                        error="JSON 格式错误"
                    });
                    return;
                }
                if(path=="/api/pair")
                {
                    lock(authGate)
                    {
                        if((DateTime.UtcNow-authWindow).TotalSeconds>=60)
                        {
                            attempts=0;
                            authWindow=DateTime.UtcNow;
                        }
                        if(attempts>=6)
                        {
                            Reply(s,429,new
                            {
                                error="尝试过多，请一分钟后重试"
                            });
                            return;
                        }
                        object code;
                        if(!body.TryGetValue("code",out code)||!(code is string)||!Equal((string)code,PairCode))
                        {
                            attempts++;
                            Reply(s,403,new
                            {
                                error="配对码不正确，请查看电脑上的 6 位数字"
                            });
                            return;
                        }
                        Reply(s,200,new
                        {
                            token=token
                        });
                        return;
                    }
                }
                object action;
                if(!body.TryGetValue("action",out action)||!(action is string))
                {
                    Reply(s,400,new
                    {
                        error="缺少操作"
                    });
                    return;
                }
                lock(authGate)
                {
                    if((DateTime.UtcNow-lastCommand).TotalMilliseconds<200)
                    {
                        Reply(s,429,new
                        {
                            error="操作太快，请稍后再试"
                        });
                        return;
                    }
                    lastCommand=DateTime.UtcNow;
                }
                try
                {
                    Music.Control((string)action);
                    Reply(s,200,new
                    {
                        ok=true
                    });
                }
                catch(ArgumentException ex)
                {
                    Reply(s,400,new
                    {
                        error=ex.Message
                    });
                }
                catch(Exception ex)
                {
                    Reply(s,409,new
                    {
                        error=ex.Message
                    });
                }
            }
        }
        bool ValidHost(string host)
        {
            Uri uri;
            if(!Uri.TryCreate("http://"+host,UriKind.Absolute,out uri)||uri.Port!=Port)return false;
            IPAddress address;
            if(!IPAddress.TryParse(uri.Host,out address))return false;
            if(IPAddress.IsLoopback(address))return true;
            foreach(var ni in NetworkInterface.GetAllNetworkInterfaces())foreach(var a in ni.GetIPProperties().UnicastAddresses)if(a.Address.Equals(address))return true;
            return false;
        }
        static bool Equal(string a,string b)
        {
            if(a==null||b==null)return false;
            int diff=a.Length^b.Length;
            for(int i=0;i<Math.Min(a.Length,b.Length);i++)diff|=a[i]^b[i];
            return diff==0;
        }
        static void Reply(NetworkStream s,int code,object data)
        {
            Write(s,code,"application/json; charset=utf-8",Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(data)));
        }
        static void Write(NetworkStream s,int code,string type,byte[] data)
        {
            string reason=code==200?"OK":code==204?"No Content":"Error";
            byte[] h=Encoding.ASCII.GetBytes("HTTP/1.1 "+code+" "+reason+"\r\nContent-Type: "+type+"\r\nContent-Length: "+data.Length+"\r\nConnection: close\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nReferrer-Policy: no-referrer\r\nContent-Security-Policy: default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; connect-src 'self'; img-src 'self' data:; base-uri 'none'; form-action 'self'; frame-ancestors 'none'\r\n\r\n");
            s.Write(h,0,h.Length);
            s.Write(data,0,data.Length);
        }
        public void Dispose()
        {
            stopping=true;
            if(listener!=null)listener.Stop();
        }
    }
}
