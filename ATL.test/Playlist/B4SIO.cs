﻿using ATL.Playlist;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace ATL.test.IO.Playlist
{
    [TestClass]
    public class B4SIO
    {
        [TestMethod]
        public void PLIO_R_B4S()
        {
            var testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/playlist.b4s", "$PATH", TestUtils.GetResourceLocationRoot(false));
            
            try
            {
                var theReader = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);
                var filePaths = theReader.FilePaths;
                Assert.IsNotInstanceOfType(theReader, typeof(ATL.Playlist.IO.DummyIO));
                Assert.AreEqual(4, filePaths.Count);
                foreach (string s in theReader.FilePaths)
                {
                    if (!s.StartsWith("http", StringComparison.InvariantCultureIgnoreCase)) Assert.IsTrue(System.IO.File.Exists(s));
                }
                foreach (Track t in theReader.Tracks)
                {
                    // Ensure the track has been parsed when it points to a file
                    if (!t.Path.StartsWith("http", StringComparison.InvariantCultureIgnoreCase)) Assert.IsTrue(t.Duration > 0);
                }
            }
            finally
            {
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
        }

        [TestMethod]
        public void PLIO_W_B4S()
        {
            IList<string> pathsToWrite = new List<string>();
            pathsToWrite.Add("aaa.mp3");
            pathsToWrite.Add("bbb.mp3");
            pathsToWrite.Add("http://this-is-a-stre.am:8405/live");

            IList<Track> tracksToWrite = new List<Track>();
            tracksToWrite.Add(new Track(Path.Combine(TestUtils.GetResourceLocationRoot() + "MP3","empty.mp3")));
            tracksToWrite.Add(new Track(Path.Combine(TestUtils.GetResourceLocationRoot() + "MOD","mod.mod")));
            tracksToWrite.Add(new Track("http://this-is-a-stre.am:8405/live"));


            string testFileLocation = TestUtils.CreateTempTestFile("test.b4s");
            try
            {
                IPlaylistIO pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);

                // Test Path writing
                pls.FilePaths = pathsToWrite;
                IList<string> parents = new List<string>();
                int index = -1;

                using (FileStream fs = new FileStream(testFileLocation, FileMode.Open))
                {
                    // Test if _no_ UTF-8 BOM has been written at the beginning of the file
                    byte[] bom = new byte[3];
                    fs.Read(bom, 0, 3);
                    Assert.IsFalse(StreamUtils.ArrEqualsArr(bom, PlaylistIO.BOM_UTF8));
                    fs.Seek(0, SeekOrigin.Begin);

                    using (XmlReader source = XmlReader.Create(fs))
                    {
                        while (source.Read())
                        {
                            if (source.NodeType == XmlNodeType.Element)
                            {
                                if (source.Name.Equals("WinampXML", StringComparison.OrdinalIgnoreCase)) parents.Add(source.Name);
                                else if (source.Name.Equals("playlist", StringComparison.OrdinalIgnoreCase) && parents.Contains("WinampXML")) parents.Add(source.Name);
                                else if (source.Name.Equals("entry", StringComparison.OrdinalIgnoreCase) && parents.Contains("playlist"))
                                {
                                    parents.Add(source.Name);
                                    index++;
                                    Assert.AreEqual(pathsToWrite[index], source.GetAttribute("Playstring"));
                                }
                            }
                        }
                    }
                }

                Assert.AreEqual(5, parents.Count);

                IList<string> filePaths = pls.FilePaths;
                Assert.AreEqual(pathsToWrite.Count, filePaths.Count);
                for (int i = 0; i < pathsToWrite.Count; i++) Assert.IsTrue(filePaths[i].EndsWith(pathsToWrite[i]));


                // Test Track writing
                pls.Tracks = tracksToWrite;
                index = -1;
                parents.Clear();

                using (FileStream fs = new FileStream(testFileLocation, FileMode.Open))
                using (XmlReader source = XmlReader.Create(fs))
                {
                    while (source.Read())
                    {
                        if (source.NodeType == XmlNodeType.Element)
                        {
                            if (source.Name.Equals("WinampXML", StringComparison.OrdinalIgnoreCase)) parents.Add(source.Name);
                            else if (source.Name.Equals("playlist", StringComparison.OrdinalIgnoreCase) && parents.Contains("WinampXML")) parents.Add(source.Name);
                            else if (source.Name.Equals("entry", StringComparison.OrdinalIgnoreCase) && parents.Contains("playlist"))
                            {
                                parents.Add(source.Name);
                                index++;
                                Assert.IsTrue(source.GetAttribute("Playstring").EndsWith(tracksToWrite[index].Path.Replace('\\','/')));
                            }
                            else if (parents.Contains("entry"))
                            {
                                if (source.Name.Equals("Name", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(tracksToWrite[index].Title, getXmlValue(source));
                                else if (source.Name.Equals("Length", StringComparison.OrdinalIgnoreCase)) Assert.AreEqual(((long)Math.Round(tracksToWrite[index].DurationMs)).ToString(), getXmlValue(source));
                            }
                        }
                    }
                }
                Assert.AreEqual(5, parents.Count);

                IList<Track> tracks = pls.Tracks;
                Assert.AreEqual(tracksToWrite.Count, tracks.Count);
                for (int i = 0; i < tracksToWrite.Count; i++) Assert.AreEqual(tracksToWrite[i].Path, tracks[i].Path);
            }
            finally
            {
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
        }

        private static string getXmlValue(XmlReader source)
        {
            source.Read();
            if (source.NodeType == XmlNodeType.Text)
            {
                return source.Value;
            }
            return "";
        }
    }
}
