﻿////////////////////////////////////////////////////////////////////////////////
// The MIT License (MIT)
//
// Copyright (c) 2015 Tim Stair
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.
////////////////////////////////////////////////////////////////////////////////


using System;
using System.IO;
using CardMaker.Data;
using CardMaker.XML;
using Support.IO;

namespace CardMaker.Events.Managers
{
    /// <summary>
    /// Handles general Project related communication between components
    /// </summary>
    public class ProjectManager
    {
        private static ProjectManager m_zProjectManager;

        public Project LoadedProject { get; private set; }

        public string ProjectFilePath { get; private set; }

        public string ProjectPath { get; private set; }

        /// <summary>
        /// Fired when a project is opened
        /// </summary>
        public event ProjectOpened ProjectOpened;

        /// <summary>
        /// Fired when the project is changed (generally a high level Layout change)
        /// </summary>
        public event ProjectUpdated ProjectUpdated;

        public static ProjectManager Instance
        {
            get
            {
                if (m_zProjectManager == null)
                {
                    m_zProjectManager = new ProjectManager(); ;
                }
                return m_zProjectManager;
            }
        }

        /// <summary>
        /// Loads the project file for use by ProjectManager event listeners
        /// </summary>
        /// <param name="sFile"></param>
        /// <returns></returns>
        public void OpenProject(string sProjectFile)
        {
            LoadedProject = LoadProject(sProjectFile);
            SetLoadedProjectFile(sProjectFile);
            if (ProjectOpened != null)
            {
                ProjectOpened(this, new ProjectEventArgs(LoadedProject, ProjectFilePath)); 
            }
        }

        /// <summary>
        /// Performs a save of the current Project
        /// </summary>
        /// <param name="sFile">The path to save</param>
        /// <param name="sOldFile">The old file path (necessary for reference updating)</param>
        /// <returns></returns>
        public bool Save(string sFile, string sOldFile)
        {
            string sProjectPath = Path.GetDirectoryName(sFile);
            string sOldProjectPath = Path.GetDirectoryName(sOldFile);

            bool bOldPathValid = !String.IsNullOrEmpty(sOldProjectPath);

            if (sProjectPath != null &&
                !sProjectPath.Equals(sOldProjectPath, StringComparison.CurrentCultureIgnoreCase))
            {
                // change the relative paths for the references
                foreach (var zLayout in LoadedProject.Layout)
                {
                    if (null != zLayout.Reference)
                    {
                        foreach (ProjectLayoutReference zReference in zLayout.Reference)
                        {
                            zReference.RelativePath = bOldPathValid
                                ? IOUtils.UpdateRelativePath(sOldProjectPath, zReference.RelativePath, sProjectPath)
                                : zReference.RelativePath =
                                    IOUtils.GetRelativePath(sProjectPath, zReference.RelativePath);
                        }
                    }
                }
            }

            if (SerializationUtils.SerializeToXmlFile(sFile, LoadedProject, CardMakerConstants.XML_ENCODING))
            {
                SetLoadedProjectFile(sFile);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Configures the instance variables related to the loaded project
        /// </summary>
        /// <param name="sProjectFile">The file path to the project</param>
        private void SetLoadedProjectFile(string sProjectFile)
        {
            ProjectFilePath = sProjectFile;
            ProjectPath = String.IsNullOrEmpty(ProjectFilePath) ? null : (Path.GetDirectoryName(sProjectFile) + Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Fires the ProjectUpdated event
        /// </summary>
        public void FireProjectUpdated()
        {
            if (null != ProjectUpdated)
            {
                ProjectUpdated(this, new ProjectEventArgs(LoadedProject, ProjectFilePath));
            }
        }

        /// <summary>
        /// Opens a project file for static usage (no events are fired)
        /// </summary>
        /// <param name="sFile">The project file to load</param>
        /// <returns></returns>
        public static Project LoadProject(string sFile)
        {
            Project zProject = null;
            // reset the collection of CardLayout objects
            if (File.Exists(sFile))
            {
                if (!SerializationUtils.DeserializeFromXmlFile(sFile, CardMakerConstants.XML_ENCODING, ref zProject))
                {
                    Logger.AddLogLine("Failed to load project. Attempting upgrade from previous version.");
                    string sContents = File.ReadAllText(sFile);
                    // Fix the previous version's mistakes!
                    sContents = sContents.Replace("xmlns=\"http://tempuri.org/Project.xsd\"", String.Empty);
                    if (!SerializationUtils.DeserializeFromXmlString(sContents, CardMakerConstants.XML_ENCODING, ref zProject))
                    {
                        Logger.AddLogLine("Failed to load project. The project file appears to be corrupt.");
                    }
                    else
                    {
                        Logger.AddLogLine("This project file is in an older format. Please save it using this version.");
                    }

                }
            }
            else
            {
                Logger.AddLogLine("No existing file specified. Loading defaults...");
                zProject = new Project
                {
                    Layout = new ProjectLayout[] { new ProjectLayout("Default") }
                };
            }
            return zProject;
        }
    }
}
