﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;

namespace AemulusModManager
{
    public static class tblPatch
    {
        private static string tblDir;

        private static byte[] SliceArray(byte[] source, int start, int end)
        {
            int length = end - start;
            byte[] dest = new byte[length];
            Array.Copy(source, start, dest, 0, length);
            return dest;
        }

        private static int Search(byte[] src, byte[] pattern)
        {
            int c = src.Length - pattern.Length + 1;
            int j;
            for (int i = 0; i < c; i++)
            {
                if (src[i] != pattern[0]) continue;
                for (j = pattern.Length - 1; j >= 1 && src[i + j] == pattern[j]; j--) ;
                if (j == 0) return i;
            }
            return -1;
        }

        
        private static void unpackTbls(string archive, string game)
        {
            string[] tbls = null;
            if (game == "Persona 4 Golden")
                tbls = new string[] { "SKILL.TBL", "UNIT.TBL", "MSG.TBL", "PERSONA.TBL", "ENCOUNT.TBL",
                    "EFFECT.TBL", "MODEL.TBL", "AICALC.TBL" };
            else if (game == "Persona 5")
                tbls = new string[] { "SKILL.TBL", "UNIT.TBL", "TALKINFO.TBL", "PERSONA.TBL", "ENCOUNT.TBL",
                    "VISUAL.TBL", "AICALC.TBL", "ELSAI.TBL", "EXIST.TBL", "ITEM.TBL", "NAME.TBL", "PLAYER.TBL" };
            else if (game == "Persona 3 FES")
                return;
            foreach (var tbl in tbls)
            {
                byte[] archiveBytes = File.ReadAllBytes(archive);
                byte[] pattern = null;
                if (game == "Persona 4 Golden")
                    pattern = Encoding.ASCII.GetBytes($"battle/{tbl}");
                else if (game == "Persona 5")
                    pattern = Encoding.ASCII.GetBytes($"table/{tbl}");
                int nameOffset = Search(archiveBytes, pattern);
                
                int tblLength = BitConverter.ToInt32(archiveBytes, nameOffset+252);
                byte[] tblBytes = SliceArray(archiveBytes, nameOffset + 256, nameOffset + 256 + tblLength);
                File.WriteAllBytes($@"{tblDir}\{tbl}", tblBytes);
                Console.WriteLine($"Unpacked {tbl}");
            }
        }
        
        private static void repackTbls(string tbl, string archive, string game)
        {
            byte[] archiveBytes = File.ReadAllBytes(archive);
            string parent = null;
            if (game == "Persona 4 Golden")
                parent = "battle";
            else if (game == "Persona 5")
                parent = "table";
            else if (game == "Persona 3 FES")
                return;
            byte[] pattern = Encoding.ASCII.GetBytes($"{parent}/{Path.GetFileName(tbl)}");
            int offset = Search(archiveBytes, pattern) + 256;
            byte[] tblBytes = File.ReadAllBytes(tbl);
            tblBytes.CopyTo(archiveBytes, offset);
            File.WriteAllBytes(archive, archiveBytes);
        }

        public static void Patch(List<string> ModList, string modDir, bool useCpk, string cpkLang, string game)
        {
            Console.WriteLine("Patching .tbl's...");
            // Check if init_free exists and return if not
            string archive = null;
            if (game == "Persona 4 Golden")
            {
                if (useCpk)
                    archive = $@"{Path.GetFileNameWithoutExtension(cpkLang)}\init_free.bin";
                else
                {
                    switch (cpkLang)
                    {
                        case "data_e.cpk":
                            archive = $@"data00004\init_free.bin";
                            break;
                        case "data.cpk":
                            archive = $@"data00001\init_free.bin";
                            break;
                        case "data_c.cpk":
                            archive = $@"data00006\init_free.bin";
                            break;
                        case "data_k.cpk":
                            archive = $@"data00005\init_free.bin";
                            break;
                        default:
                            archive = $@"data00004\init_free.bin";
                            break;
                    }
                }
            }
            else if (game == "Persona 5")
                archive = @"battle\table.pac";
            if (game != "Persona 3 FES")
            {
                if (!File.Exists($@"{modDir}\{archive}"))
                {
                    if (File.Exists($@"Original\{game}\{archive}"))
                    {
                        Directory.CreateDirectory($@"{modDir}\{Path.GetDirectoryName(archive)}");
                        File.Copy($@"Original\{game}\{archive}", $@"{modDir}\{archive}", true);
                        Console.WriteLine($"[INFO] Copied over {archive} from Original directory.");
                    }
                    else
                    {
                        Console.WriteLine($"[WARNING] {archive} not found in output directory or Original directory.");
                        return;
                    }
                }
            
                tblDir = $@"{modDir}\{Path.ChangeExtension(archive, null)}_tbls";
                Directory.CreateDirectory(tblDir);
                // Unpack init_free
                Console.WriteLine($"[INFO] Unpacking tbl's from {archive}...");
                unpackTbls($@"{modDir}\{archive}", game);
            }
            // Keep track of which tables are edited
            List<string> editedTables = new List<string>();

            // Load EnabledPatches in order
            foreach (string dir in ModList)
            {
                Console.WriteLine($"[INFO] Searching for/applying tblpatches in {dir}...");
                if (!Directory.Exists($@"{dir}\tblpatches"))
                {
                    Console.WriteLine($"[INFO] No tblpatches folder found in {dir}");
                    continue;
                }
                foreach (var t in Directory.EnumerateFiles($@"{dir}\tblpatches", "*.tblpatch", SearchOption.TopDirectoryOnly).Union
                       (Directory.EnumerateFiles(dir, "*.tblpatch", SearchOption.TopDirectoryOnly)))
                {
                    byte[] file = File.ReadAllBytes(t);
                    string fileName = Path.GetFileName(t);
                    //Console.WriteLine($"[INFO] Loading {fileName}");
                    if (file.Length < 12)
                    {
                        Console.WriteLine("[ERROR] Improper .tblpatch format.");
                        continue;
                    }

                    // Name of tbl file
                    string tblName = Encoding.ASCII.GetString(SliceArray(file, 0, 3));
                    // Offset to start overwriting at
                    byte[] byteOffset = SliceArray(file, 3, 11);
                    // Reverse endianess
                    Array.Reverse(byteOffset, 0, 8);
                    long offset = BitConverter.ToInt64(byteOffset, 0);
                    // Contents is what to replace
                    byte[] fileContents = SliceArray(file, 11, file.Length);

                    /*
                    * P4G TBLS:
                    * SKILL - SKL
                    * UNIT - UNT
                    * MSG - MSG
                    * PERSONA - PSA
                    * ENCOUNT - ENC
                    * EFFECT - EFF
                    * MODEL - MDL
                    * AICALC - AIC
                    *
                    * P3F TBLS:
                    * AICALC - AIC
                    * AICALC_F - AIF
                    * EFFECT - EFF
                    * ENCOUNT - ENC
                    * ENCOUNT_F - ENF
                    * MODEL - MDL
                    * MSG - MSG
                    * PERSONA - PSA
                    * PERSONA_F - PSF
                    * SKILL - SKL
                    * SKILL_F - SKF
                    * UNIT - UNT
                    * UNIT_F - UNF
                    * 
                    * P5 TBLS:
                    * AICALC - AIC
                    * ELSAI - EAI
                    * ENCOUNT - ENC
                    * EXIST - EXT
                    * ITEM - ITM
                    * NAME - NME
                    * PERSONA - PSA
                    * PLAYER - PLY
                    * SKILL - SKL
                    * TALKINFO - TKI
                    * UNIT - UNT
                    * VISUAL - VSL
                    */

                    switch (tblName)
                    {
                        case "SKL":
                            tblName = "SKILL.TBL";
                            break;
                        case "UNT":
                            tblName = "UNIT.TBL";
                            break;
                        case "MSG":
                            tblName = "MSG.TBL";
                            if (game == "Persona 5")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "PSA":
                            tblName = "PERSONA.TBL";
                            break;
                        case "ENC":
                            tblName = "ENCOUNT.TBL";
                            break;
                        case "EFF":
                            tblName = "EFFECT.TBL";
                            if (game == "Persona 5")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "MDL":
                            tblName = "MODEL.TBL";
                            if (game == "Persona 5")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "AIC":
                            tblName = "AICALC.TBL";
                            break;
                        case "AIF":
                            tblName = "AICALC_F.TBL";
                            if (game != "Persona 3 FES")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "ENF":
                            tblName = "ENCOUNT_F.TBL";
                            if (game != "Persona 3 FES")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "PSF":
                            tblName = "PERSONA_F.TBL";
                            if (game != "Persona 3 FES")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "SKF":
                            tblName = "SKILL_F.TBL";
                            if (game != "Persona 3 FES")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "UNF":
                            tblName = "UNIT_F.TBL";
                            if (game != "Persona 3 FES")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "EAI":
                            tblName = "ELSAI.TBL";
                            if (game != "Persona 5")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "EXT":
                            tblName = "EXIST.TBL";
                            if (game != "Persona 5")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "ITM":
                            tblName = "ITEM.TBL";
                            if (game != "Persona 5")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "NME":
                            tblName = "NAME.TBL";
                            if (game != "Persona 5")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "PLY":
                            tblName = "PLAYER.TBL";
                            if (game != "Persona 5")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "TKI":
                            tblName = "TALKINFO.TBL";
                            if (game != "Persona 5")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        case "VSL":
                            tblName = "VISUAL.TBL";
                            if (game != "Persona 5")
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in {game}, skipping");
                                continue;
                            }
                            break;
                        default:
                            Console.WriteLine($"[ERROR] Unknown tbl name for {t}.");
                            continue;
                    }

                    // Keep track of which TBL's were edited
                    if (!editedTables.Contains(tblName))
                        editedTables.Add(tblName);

                    // TBL file to edit
                    if (game != "Persona 3 FES")
                    {
                        string unpackedTblPath = $@"{tblDir}\{tblName}";
                        byte[] tblBytes = File.ReadAllBytes(unpackedTblPath);
                        fileContents.CopyTo(tblBytes, offset);
                        File.WriteAllBytes(unpackedTblPath, tblBytes);
                    }
                    else
                    {
                        if (!File.Exists($@"{modDir}\BTL\BATTLE\{tblName}"))
                        {
                            if (File.Exists($@"Original\{game}\BTL\BATTLE\{tblName}") && !File.Exists($@"{modDir}\BTL\BATTLE\{tblName}"))
                            {
                                Directory.CreateDirectory($@"{modDir}\BTL\BATTLE");
                                File.Copy($@"Original\{game}\BTL\BATTLE\{tblName}", $@"{modDir}\BTL\BATTLE\{tblName}", true);
                                Console.WriteLine($"[INFO] Copied over {tblName} from Original directory.");
                            }
                            else if (!File.Exists($@"Original\{game}\BTL\BATTLE\{tblName}") && !File.Exists($@"{modDir}\BTL\BATTLE\{tblName}"))
                            {
                                Console.WriteLine($"[WARNING] {tblName} not found in output directory or Original directory.");
                                continue;
                            }
                            string tblPath = $@"{modDir}\BTL\BATTLE\{tblName}";
                            byte[] tblBytes = File.ReadAllBytes(tblPath);
                            fileContents.CopyTo(tblBytes, offset);
                            File.WriteAllBytes(tblPath, tblBytes);
                        }
                    }
                }

                Console.WriteLine($"[INFO] Applied patches from {dir}");
                
            }
            if (game != "Persona 3 FES")
            {
                // Replace each edited TBL's
                foreach (string u in editedTables)
                {
                    Console.WriteLine($"[INFO] Replacing {u} in {archive}");
                    repackTbls($@"{tblDir}\{u}", $@"{modDir}\{archive}", game);
                }

                Console.WriteLine($"[INFO] Deleting temp tbl folder...");
                // Delete all unpacked files
                Directory.Delete(tblDir, true);
            }
            Console.WriteLine("[INFO] Finished patching tbl's!");
        }
    }

}