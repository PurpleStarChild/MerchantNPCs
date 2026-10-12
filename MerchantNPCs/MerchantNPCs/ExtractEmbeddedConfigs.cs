using BepInEx;
using System;
using System.IO;
using System.Reflection;

namespace MerchantNPCs
{
    internal partial class MerchantNPCsPlugin
    {
        /// <summary>
        /// Extracts embedded configuration files to the BepInEx config folder
        /// </summary>
        private void ExtractEmbeddedConfigs()
        {
            try
            {
                // Debug: List all embedded resources
                string[] allResources = Assembly.GetExecutingAssembly().GetManifestResourceNames();
                //Logger.LogInfo($"Found {allResources.Length} embedded resources:");
                foreach (string resource in allResources)
                {
                    //Logger.LogInfo($"  - {resource}");
                }

                // Get the config directory path
                string configDir = Path.Combine(Paths.ConfigPath, "MerchantNPCs");
                
                // Create the directory if it doesn't exist
                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                    //Logger.LogInfo($"Created config directory: {configDir}");
                }
                
                // List of config files to extract
                string[] configFiles = new string[] 
                { 
                    "Meadows.txt", 
                    "BlackForest.txt", 
                    "Swamp.txt", 
                    "Mountains.txt", 
                    "Plains.txt", 
                    "Mistlands.txt", 
                    "Ashlands.txt", 
                    "DeepNorth.txt", 
                    "Extra.txt" 
                };
                
                // Extract each config file
                foreach (string fileName in configFiles)
                {
                    // Try different resource name formats
                    string[] possibleResourceNames = new string[] {
                        $"MerchantNPCs.Configs.{fileName}",
                        $"Configs.{fileName}",
                        fileName
                    };
                    
                    string targetPath = Path.Combine(configDir, fileName);
                    bool resourceFound = false;
                    
                    // Only extract if the file doesn't exist
                    if (!File.Exists(targetPath))
                    {
                        // Try each possible resource name
                        foreach (string resourceName in possibleResourceNames)
                        {
                            using (Stream resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                            {
                                if (resourceStream != null)
                                {
                                    using (FileStream fileStream = new FileStream(targetPath, FileMode.Create))
                                    {
                                        resourceStream.CopyTo(fileStream);
                                    }
                                    //Logger.LogInfo($"Extracted config file: {fileName} from resource {resourceName}");
                                    resourceFound = true;
                                    break;
                                }
                            }
                        }
                        
                        if (!resourceFound)
                        {
                            // If resource not found, copy from the project directory as a fallback
                            string projectConfigPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "..\\..\\..\\Configs", fileName);
                            if (File.Exists(projectConfigPath))
                            {
                                File.Copy(projectConfigPath, targetPath);
                                //Logger.LogInfo($"Copied config file from project directory: {fileName}");
                            }
                            else
                            {
                                Logger.LogWarning($"Could not find config file {fileName} as embedded resource or in project directory");
                                
                                // As a last resort, create an empty config file
                                File.WriteAllText(targetPath, $"# {fileName.Replace(".txt", "")} Merchant Items\n# Format: item:price\n\n# Add your items here\n");
                                //Logger.LogInfo($"Created empty config file: {fileName}");
                            }
                        }
                    }
                    else
                    {
                        // Logger.LogDebug($"Config file already exists: {fileName}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error extracting config files: {ex.Message}");
            }
        }
    }
}
