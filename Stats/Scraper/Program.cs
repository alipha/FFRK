using DataAccess;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Scraper
{
    public class Program
    {
        private static FFRKEntities database = new FFRKEntities();

        public static void Main(string[] args)
        {
            //DownloadItemPages();
            //ParseCharacters();
            //ParseItemPages();
            //DownloadItemImages();
            //DownloadSoulBreakPages();
            ParseSoulBreakPages();

            Console.ReadLine();
        }

        private static void DownloadItemPages()
        {
            using (var client = new WebClient())
            {
                for (int i = 0; i <= 471; i++)
                {
                    try
                    {
                        client.DownloadFile("http://www.ffrkguide.com/equipmentDetails.aspx?i=" + i, @"item_pages\i" + i + ".html");
                        Console.WriteLine("Downloaded item " + i);
                    } 
                    catch(Exception ex)
                    {
                        Console.WriteLine("Error downloading item " + i + ": " + ex.Message);
                    }
                }
            }
        }

        private static void DownloadSoulBreakPages()
        {
            List<SoulBreak> soulBreaks = database.SoulBreaks.ToList();

            using (var client = new WebClient())
            {
                foreach(SoulBreak soulBreak in soulBreaks)
                {
                    try
                    {
                        client.DownloadFile("http://www.ffrkguide.com/soulbreak.aspx?n=" + soulBreak.TempLink, @"soulbreak_pages\" + soulBreak.TempLink + ".html");
                        Console.WriteLine("Downloaded " + soulBreak.Name);
                    } 
                    catch(Exception ex)
                    {
                        Console.WriteLine("Error downloading " + soulBreak.Name + ": " + ex.Message);
                    }
                }
            }
        }

        private static void ParseCharacters()
        {
            IDictionary<string, Game> games = database.Games.ToDictionary(g => g.TempName);

            var html = new HtmlDocument();
            html.Load("stats.html");

            HtmlNodeCollection rows = html.DocumentNode.SelectNodes("//*[@id='dt_basic']/tbody/tr");

            using (var client = new WebClient())
            {
                foreach (HtmlNode row in rows)
                {
                    string characterName = row.SelectSingleNode("td[3]").InnerText;
                    string gameName = row.SelectSingleNode("td[4]").InnerText;

                    if (gameName == "FFRK")
                        gameName = "Core";

                    if (games.ContainsKey(gameName))
                    {
                        /*
                        Character character = new Character
                        {
                            Name = characterName,
                            GameName = games[gameName].Name
                        };

                        database.Characters.Add(character);
                         */

                        try
                        {
                            characterName = characterName.Replace("&middot; ", " (").Replace(" &middot;", ")");
                            Character character = database.Characters.Find(characterName);

                            character.ImageName = row.SelectSingleNode("td[2]/img").GetAttributeValue("src", null).Replace("img/ffrk/", "");
                            character.JobName = row.SelectSingleNode("td[5]").InnerText;
                            character.Health = int.Parse(row.SelectSingleNode("td[6]").InnerText);
                            character.Attack = int.Parse(row.SelectSingleNode("td[7]").InnerText);
                            character.Defense = int.Parse(row.SelectSingleNode("td[8]").InnerText);
                            character.Magic = int.Parse(row.SelectSingleNode("td[9]").InnerText);
                            character.Mind = int.Parse(row.SelectSingleNode("td[10]").InnerText);
                            character.Resistance = int.Parse(row.SelectSingleNode("td[11]").InnerText);
                            character.Speed = int.Parse(row.SelectSingleNode("td[12]").InnerText);

                            database.Entry(character).State = EntityState.Modified;

                            try
                            {
                                client.DownloadFile("http://chapter731.net/ffrk/img/ffrk/" + character.ImageName, @"images\characters\" + character.ImageName);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Error downloading " + character.ImageName + ": " + ex.Message);
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error while updating " + characterName + ": " + ex.Message);
                        }
                    }
                    else
                    {
                        Console.WriteLine("No match for " + gameName + " while parsing " + characterName);
                    }
                }
            }

            database.SaveChanges();
        }

        private static void ParseItemPages()
        {
            IDictionary<string, ItemType> types = new Dictionary<string, ItemType>();
            var idRegex = new Regex(@"\d+");
            var headerRegex = new Regex(@"(.*) \((\w+)\)");

            string[] pages = Directory.GetFiles(@"item_pages\");

            IList<Item> items = new List<Item>();

            foreach(string page in pages)
            {
                var html = new HtmlDocument();
                html.Load(page);

                string header = html.DocumentNode.SelectSingleNode("//*[@id='bodyPH_title']").InnerText;
                HtmlNode mainDiv = html.DocumentNode.SelectSingleNode("//div[contains(@style, 'table-cell')]");
                HtmlNodeCollection tables = mainDiv.SelectNodes("//table");
                HtmlNodeCollection imgs = mainDiv.SelectNodes("//img");
                string type = tables.First().SelectSingleNode("tr/td[2]").InnerText;

                if (type == "Other" || type == "Orb")
                    continue;

                if (type == "Swords")
                    type = "Sword";
                if (type == "Fist Weapon")
                    type = "Fist";
                if (type == "Thrown Weapon")
                    type = "Thrown";

                ItemType itemType;

                if (types.ContainsKey(type))
                {
                    itemType = types[type];
                }
                else
                {
                    types[type] = itemType = new ItemType
                    {
                        Name = type,
                        SortOrder = 0
                    };
                }

                Match match = headerRegex.Match(header);
                string itemName = header;
                string gameName = "Core";

                if (match.Success)
                {
                    itemName = match.Groups[1].Value;
                    gameName = match.Groups[2].Value;
                }
                else
                {
                    Console.WriteLine("No game specified: " + header);
                }

                HtmlNode effect = mainDiv.SelectFirstNodeOrNull("//span[contains(@style, 'Open Sans Condensed')]");
                HtmlNode soulBreakName = mainDiv.SelectFirstNodeOrNull("//img[contains(@src, 'soulBreaks')]");

                SoulBreak soulBreak = null;

                if (soulBreakName != null && !page.Contains("419") && !page.Contains("432") && !page.Contains("450") && !page.Contains("463"))
                {
                    HtmlNode img = mainDiv.SelectFirstNodeOrNull("//img[contains(@src, 'characters')]");
                    string characterName = (img != null ? img.GetAttributeValue("title", null) : null);

                    if(characterName == "Cecil")
                        characterName = (img.GetAttributeValue("src", null).Contains("cecilp") ? "Cecil (Paladin)" : "Cecil (Dark Knight)");

                    soulBreak = new SoulBreak
                    {
                        Name = (page.Contains("i383") ? "Sentinel's Grimoire" : soulBreakName.GetAttributeValue("title", null)),
                        CharacterName = characterName,
                        //Gauge = ,
                        Effect = ""  // TODO
                    };
                }

                IDictionary<string, int?> attributes = new Dictionary<string, int?>();
                IDictionary<string, int?> maxAttributes = new Dictionary<string, int?>();
                var validAttributes = new string[]{ "Attack", "Defense", "Magic", "Resistance", "Mind", "Accuracy", "Evasion" };

                if (tables.Count > 1)
                {
                    foreach (HtmlNode row in tables[1].SelectNodes("tr"))
                    {
                        string attributeName = row.SelectSingleNode("td[1]").InnerText;

                        if (attributeName == "Attribute")
                            continue;

                        if (!validAttributes.Contains(attributeName))
                            Console.WriteLine("Invalid attribute: " + attributeName);

                        attributes[attributeName] = int.Parse(row.SelectSingleNode("td[2]").InnerText);
                        maxAttributes[attributeName] = int.Parse(row.SelectSingleNode("td[3]").InnerText);
                    }
                }

                var item = new Item
                {
                    Id = int.Parse(idRegex.Match(page).Groups[0].Value),
                    Name = itemName,
                    GameName = gameName,
                    //Game = ,
                    //Rarity = ,
                    //TypeName = type,
                    ItemType = itemType,
                    Effect = (effect != null ? effect.InnerText : null),
                    //SoulBreakName = (soulBreak != null ? soulBreak.Name : null),
                    SoulBreak = soulBreak,
                    Attack = attributes.GetValueOrDefault("Attack"),
                    AttackMax = maxAttributes.GetValueOrDefault("Attack"),
                    Defense = attributes.GetValueOrDefault("Defense"),
                    DefenseMax = maxAttributes.GetValueOrDefault("Defense"),
                    Magic = attributes.GetValueOrDefault("Magic"),
                    MagicMax = maxAttributes.GetValueOrDefault("Magic"),
                    Resistance = attributes.GetValueOrDefault("Resistance"),
                    ResistanceMax = maxAttributes.GetValueOrDefault("Resistance"),
                    Mind = attributes.GetValueOrDefault("Mind"),
                    MindMax = maxAttributes.GetValueOrDefault("Mind"),
                    Accuracy = attributes.GetValueOrDefault("Accuracy"),
                    AccuracyMax = maxAttributes.GetValueOrDefault("Accuracy"),
                    Evade = attributes.GetValueOrDefault("Evasion"),
                    EvadeMax = maxAttributes.GetValueOrDefault("Evasion")
                };

                database.Items.Add(item);

                try
                {
                    database.SaveChanges();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error saving item: " + ex.Message);
                }
                //items.Add(item);
            }

            //foreach (var type in types.Select(t => t.Key))
            //    Console.WriteLine(type);


            //var games = database.Games.ToList();

            //foreach (var item in items)
            //    if (!games.Any(g => g.Name == item.GameName))
            //        Console.WriteLine("*" + item.GameName + "=" + item.TempId);

            //var characters = database.Characters.ToList();

            //foreach (var item in items.Where(i => i.SoulBreak != null))
            //    if (!characters.Any(c => c.Name == item.SoulBreak.CharacterName))
            //        Console.WriteLine("#" + item.SoulBreak.CharacterName + "=" + item.TempId);
        }

        private static void DownloadItemImages()
        {
            var idRegex = new Regex(@"\d+");
            string[] pages = Directory.GetFiles(@"item_pages\");

            using (var client = new WebClient())
            {
                foreach (string page in pages)
                {
                    int id = int.Parse(idRegex.Match(page).Groups[0].Value);

                    var html = new HtmlDocument();
                    html.Load(page);

                    string header = html.DocumentNode.SelectSingleNode("//*[@id='bodyPH_title']").InnerText;
                    HtmlNode mainDiv = html.DocumentNode.SelectSingleNode("//div[contains(@style, 'table-cell')]");
                    HtmlNodeCollection tables = mainDiv.SelectNodes("//table");
                    string type = tables.First().SelectSingleNode("tr/td[2]").InnerText;

                    if (type == "Other" || type == "Orb")
                        continue;

                    try
                    {
                        Item item = database.Items.Find(id);

                        item.ImageName = mainDiv.SelectSingleNode("//img[contains(@src, 'items')]").GetAttributeValue("src", null).Replace("images/items/", "");
                        database.Entry(item).State = EntityState.Modified;

                        client.DownloadFile("http://www.ffrkguide.com/images/items/" + item.ImageName, @"images\items\" + item.ImageName);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error updating item for " + id + ": " + ex.Message);
                    }

                    if (id != 419 && id != 432 && id != 450 && id != 463)
                    {
                        HtmlNode soulBreakLink = mainDiv.SelectFirstNodeOrNull("//a[contains(@href, 'soulbreak.aspx')]");

                        if (soulBreakLink != null)
                        {
                            try
                            {
                                HtmlNode soulBreakImg = soulBreakLink.SelectSingleNode("img");

                                SoulBreak soulBreak = database.SoulBreaks.Find(soulBreakImg.GetAttributeValue("title", null));
                                soulBreak.ImageName = soulBreakImg.GetAttributeValue("src", null).Replace("images/soulBreaks/", "");
                                soulBreak.TempLink = soulBreakLink.GetAttributeValue("href", null).Replace("soulbreak.aspx?n=", "");
                                database.Entry(soulBreak).State = EntityState.Modified;
                            
                                client.DownloadFile("http://www.ffrkguide.com/images/soulBreaks/" + soulBreak.ImageName, @"images\soulbreaks\" + soulBreak.ImageName);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine("Error updating soul break for " + id + ": " + ex.Message);
                            }
                        }
                    }
                }

                try
                {
                    database.SaveChanges();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error saving: " + ex.Message);
                }
            }
        }

        private static void ParseSoulBreakPages()
        {
            string[] pages = Directory.GetFiles(@"soulbreak_pages\");

            using (var client = new WebClient())
            {
                foreach (string page in pages)
                {
                    string tempLink = page.Replace(@"soulbreak_pages\", "").Replace(".html", "");

                    var html = new HtmlDocument();
                    html.Load(page);

                    HtmlNode mainDiv = html.DocumentNode.SelectSingleNode("//div[contains(@style, 'table-cell')]");

                    try
                    {
                        SoulBreak soulBreak = database.SoulBreaks.Single(sb => sb.TempLink == tempLink);

                        soulBreak.Effect = mainDiv.SelectSingleNode("//span[contains(@style, 'Open Sans Condensed')]").InnerText;
                        soulBreak.Gauge = int.Parse(mainDiv.SelectSingleNode("//table/tr/td[2]").InnerText);

                        database.Entry(soulBreak).State = EntityState.Modified;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error updating soul break for " + tempLink + ": " + ex.Message);
                    }
                }

                try
                {
                    database.SaveChanges();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error saving: " + ex.Message);
                }
            }
        }
    }
}
