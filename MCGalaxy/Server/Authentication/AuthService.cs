﻿/*
    Copyright 2015-2024 MCGalaxy
    
    Dual-licensed under the Educational Community License, Version 2.0 and
    the GNU General Public License, Version 3 (the "Licenses"); you may
    not use this file except in compliance with the Licenses. You may
    obtain a copy of the Licenses at
    
    https://opensource.org/license/ecl-2-0/
    https://www.gnu.org/licenses/gpl-3.0.html
    
    Unless required by applicable law or agreed to in writing,
    software distributed under the Licenses are distributed on an "AS IS"
    BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express
    or implied. See the Licenses for the specific language governing
    permissions and limitations under the Licenses.
 */
using System;
using System.Collections.Generic;
using System.IO;

namespace MCGalaxy.Authentication
{
    public class AuthService
    {
        public static List<AuthService> Services = new List<AuthService>();
        
        public string URL;
        public string Salt;
        public string NameSuffix = "";
        public string SkinPrefix = "";
        public bool MojangAuth;
        
        public virtual void AcceptPlayer(Player p) {
            p.VerifiedVia  = URL;
            p.verifiedName = true;
            p.SkinName     = SkinPrefix + p.SkinName;
            
            string suffix  = NameSuffix;
            p.name        += suffix;
            p.truename    += suffix;
            p.DisplayName += suffix;
        }
       
        
        public static AuthService GetOrCreate(string url, bool canSave = true)
        {
            foreach (AuthService s in Services)
            {
                if (s.URL.CaselessEq(url)) return s;
            }
            
            AuthService service = new AuthService();
            service.URL  = url;
            service.Salt = Server.GenerateSalt();
            Services.Add(service);
            
            // TODO: Maybe seperate method instead
            if (!canSave) return service;
            try {
                SaveServices();
            } catch (Exception ex) {
                Logger.LogError("Error saving authservices.properties", ex);
            }
            return service;
        }
 
        
        /// <summary> Updates list of authentication services from authservices.properties </summary>
        internal static void UpdateList() {
            AuthService cur = null;
            PropertiesFile.Read(Paths.AuthServicesFile, ref cur, ParseProperty, '=', true);
           
            // NOTE: Heartbeat.ReloadDefault will call GetOrCreate for all of the 
            //  URLs specified in the HeartbeatURL server configuration property
            // Therefore it is unnecessary to create default AuthServices here
            //  (e.g. for when authservices.properties is empty or is missing a URL)
        }
        
        static void ParseProperty(string key, string value, ref AuthService cur) {
            if (key.CaselessEq("URL")) {
                cur = GetOrCreate(value, false);
            } else if (key.CaselessEq("name-suffix")) {
                if (cur == null) return;
                cur.NameSuffix = value;
            } else if (key.CaselessEq("skin-prefix")) {
                if (cur == null) return;
                cur.SkinPrefix = value;
            } else if (key.CaselessEq("mojang-auth")) {
                if (cur == null) return;
                bool.TryParse(value, out cur.MojangAuth);
            }
        }
        
        static void SaveServices() {
            using (StreamWriter w = new StreamWriter(Paths.AuthServicesFile)) {
                w.WriteLine("# Authentication services configuration");
                w.WriteLine("#   There is no reason to modify these configuration settings, unless the server has been configured");
                w.WriteLine("#    to send heartbeats to multiple authentication services (e.g. both ClassiCube.net and BetaCraft.uk)");
                w.WriteLine("#   DO NOT EDIT THIS FILE UNLESS YOU KNOW WHAT YOU ARE DOING");
                w.WriteLine();
                w.WriteLine("#URL = string");
                w.WriteLine("#   URL of the authentication service the following settings apply to");
                w.WriteLine("#   (this should be the same as one of the heartbeat URLs specified in server.properties)");
                w.WriteLine("#name-suffix = string");
                w.WriteLine("#   Characters that are appended to usernames of players that login through the authentication service");
                w.WriteLine("#   (used to prevent username collisions between authentication services that would otherwise occur)");
                w.WriteLine("#skin-prefix = string");
                w.WriteLine("#   Characters that are prefixed to skin name of players that login through the authentication service");
                w.WriteLine("#   (used to ensure players from other authentication services see the correct skin)");
                w.WriteLine("#mojang-auth = boolean");
                w.WriteLine("#   Whether to try verifying users using Mojang's authentication servers if mppass verification fails");
                w.WriteLine("#   NOTE: This should only be used for the Betacraft.uk authentication service");
                w.WriteLine();
                
                foreach (AuthService service in Services)
                {
                    w.WriteLine("URL = " + service.URL);
                    w.WriteLine("name-suffix = " + service.NameSuffix);
                    w.WriteLine("skin-prefix = " + service.SkinPrefix);
                    w.WriteLine("mojang-auth = " + service.MojangAuth);
                    w.WriteLine();
                }
            }
        }
    }
}