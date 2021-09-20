using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace HARExtractor
{
	class Program
	{
		static void Main(string[] args)
		{
			// args[0] - source file

			if (args.Length < 1)
			{
				Console.WriteLine("HAR Extractor");
				Console.WriteLine("\n\nUsage: HARExtractor.exe file.har");
				Console.WriteLine("\n\n\n\nERROR: No input file was given. Press any key to exit.");
				Console.ReadLine();
				Environment.Exit(0);
			}

			using (FileStream fsSource = File.Open(args[0], FileMode.Open, FileAccess.Read))
			{
				using (StreamReader sr = new StreamReader(fsSource))
				{
					JObject json = JObject.Parse(sr.ReadToEnd());
					JToken entries = json["log"]["entries"];
					Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();

					foreach (JObject entryData in entries)
					{
						byte[] octetData = new byte[] { };
						string stringData = "";
						string resourceType = entryData["_resourceType"].ToString();
						JObject content = entryData["response"]["content"].ToObject<JObject>();
						string mimeType = entryData["response"]["content"]["mimeType"].ToString();

						if ((resourceType == "xhr") || (resourceType == "image"))
						{
							if (
								(mimeType == "application/octet-stream")
								|| (mimeType == "binary/octet-stream")
								|| (mimeType == "image/png")
							)
							{
								if (content.ContainsKey("text"))
								{
									JToken d = entryData["response"]["content"]["text"];
									if (content.ContainsKey("encoding") && (entryData["response"]["content"]["encoding"].ToString() == "base64"))
									{
										octetData = Convert.FromBase64String(d.ToString());
									}
									else
									{
										stringData = d.ToString();
									}
								}
							}
						}

						string url = entryData["request"]["url"].ToString();
						string name = "";
						string[] urlParts = url.Split(Convert.ToChar("/"));
						foreach (string part in urlParts)
						{
							if (
								   part.Contains(".glb")
								|| part.Contains(".gltf")
								|| part.Contains(".bin")
								|| part.Contains(".drc")
								|| part.Contains(".dds")
								|| part.Contains(".png")
								|| part.Contains(".jpg")
								|| part.Contains(".jpeg")
								|| part.Contains(".webp")
								|| part.Contains(".env")
							)
							{
								string[] nameLines = part.Split(Convert.ToChar("?"));
								name = nameLines[0].Replace("%20", " "); // replace some mnemonic codes
								Console.WriteLine(name);
							}
						}

						if (!files.ContainsKey(name))
						{
							files.Add(name, octetData);
						}
					}

					DirectoryInfo di = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
					if (di.Exists)
					{
						DirectoryInfo di2 = di.CreateSubdirectory("Unpacked");
						Directory.SetCurrentDirectory(di2.FullName);

						foreach (KeyValuePair<string, byte[]> kvp in files)
						{
							// skip zero-sized files
							if (kvp.Value.Length > 0)
							{
								// auto-rename unnamed files
								string finalName = "";
								if (kvp.Key == "")
								{
									finalName = $"0x{kvp.Value.Length:X}";
								}
								else
								{
									finalName = kvp.Key;
								}

								File.WriteAllBytes(finalName, kvp.Value);
							}
						}
					}
				}
			}

			Console.WriteLine("Finished. Press any key to exit.");
			Console.ReadLine();
			Environment.Exit(0);
		}
	}
}
