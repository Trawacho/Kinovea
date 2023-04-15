﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Forms;
using Kinovea.Video;
using Kinovea.Services;
using Kinovea.ScreenManager.Languages;

namespace Kinovea.ScreenManager
{
    /// <summary>
    /// This is the router to specialized exporters for the different video export formats.
    /// The "formats" at this level are the different types of video export we support (normal, slideshow, with pauses on key images),
    /// not the final file format. All exporters should be able to export to all the supported file formats,
    /// which will be chosen in the save file dialog.
    /// </summary>
    public class VideoExporter
    {
        private const double maxInterval = 1000.0 / 8.0;
        private readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public void Export(VideoExportFormat format, PlayerScreen player1, PlayerScreen player2)
        {
            if (player1 == null)
                return;

            // Special case for video filter with custom save mechanics.
            if (format == VideoExportFormat.Video &&
                player1.ActiveVideoFilterType != VideoFilterType.None &&
                player1.FrameServer.Metadata.ActiveVideoFilter.CanExportVideo)
            {
                player1.FrameServer.Metadata.ActiveVideoFilter.ExportVideo(player1.view);
                return;
            }

            // Immediately get a file name to save to.
            // Any configuration of the save happens later.
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = ScreenManagerLang.CommandExportVideo_FriendlyName;
            sfd.RestoreDirectory = true;
            sfd.Filter = FilesystemHelper.SaveVideoFilter();
            sfd.FilterIndex = FilesystemHelper.GetFilterIndex(sfd.Filter, PreferencesManager.PlayerPreferences.VideoFormat);
            sfd.FileName = SuggestFilename(format, player1, player2);

            if (sfd.ShowDialog() != DialogResult.OK || string.IsNullOrEmpty(sfd.FileName))
                return;

            if (sfd.FileName == player1.FrameServer.VideoReader.FilePath)
            {
                // Overwriting the original file is disallowed.
                // It causes data loss and we can't do it anyway since we need it to pull the frames.
                log.ErrorFormat("Cannot export video onto itself.");
                return;
            }

            if (!FilesystemHelper.CanWrite(sfd.FileName))
            {
                log.ErrorFormat("Cannot write to the output video file.");
                return;
            }

            // Save the new preferred image format.
            PreferencesManager.PlayerPreferences.VideoFormat = FilesystemHelper.GetVideoFormat(sfd.FileName);
            PreferencesManager.Save();

            SavingSettings s = new SavingSettings();
            Metadata metadata = player1.FrameServer.Metadata;

            try
            {
                switch (format)
                {
                    case VideoExportFormat.Video:
                        
                        // Show a configuration dialog.
                        FormConfigureExportVideo fcev = new FormConfigureExportVideo(player1);
                        fcev.StartPosition = FormStartPosition.CenterScreen;
                        if (fcev.ShowDialog() != DialogResult.OK)
                        {
                            fcev.Dispose();
                            return;
                        }

                        bool useSlowMotion = fcev.UseSlowMotion;
                        fcev.Dispose();

                        s.Section = new VideoSection(metadata.SelectionStart, metadata.SelectionEnd);
                        s.KeyframesOnly = false;
                        s.File = sfd.FileName;
                        s.ImageRetriever = player1.view.GetFlushedImage;
                        
                        // Output framerate.
                        double frameInterval = useSlowMotion ? player1.view.PlaybackFrameInterval : metadata.BaselineFrameInterval;
                        s.OutputIntervalMilliseconds = frameInterval;

                        // Frame duplication: if slower than 8 fps, start duplicating frames.
                        s.Duplication = 1;
                        if (s.OutputIntervalMilliseconds > maxInterval)
                        {
                            s.Duplication = (int)Math.Ceiling(s.OutputIntervalMilliseconds / maxInterval);
                            s.OutputIntervalMilliseconds = s.OutputIntervalMilliseconds / s.Duplication;
                        }

                        // Total frames
                        int totalFrames = (int)((metadata.SelectionEnd - metadata.SelectionStart) / metadata.AverageTimeStampsPerFrame) + 1;
                        s.EstimatedTotal = totalFrames * s.Duplication;

                        ExporterVideo exporterVideo = new ExporterVideo();
                        exporterVideo.Export(s, player1);
                        break;

                    case VideoExportFormat.VideoSlideShow:

                        // Show a configuration dialog.


                        break;

                    case VideoExportFormat.VideoWithPauses:

                        break;
                
                }

                
            }
            catch (Exception e)
            {
                log.ErrorFormat("Exception encountered while exporting video.", e);
            }
        }

        /// <summary>
        /// Returns a suggested filename (without directory nor extension), to be used in the save file dialog.
        /// </summary>
        private string SuggestFilename(VideoExportFormat format, PlayerScreen player1, PlayerScreen player2)
        {
            string filename = "";
            string videoFilePath = player1.FrameServer.VideoReader.FilePath;
            switch (format)
            {
                case VideoExportFormat.Video:
                case VideoExportFormat.VideoSlideShow:
                case VideoExportFormat.VideoWithPauses:
                    // Same as video name.
                    filename = Path.GetFileNameWithoutExtension(videoFilePath);
                    break;
                case VideoExportFormat.SideBySide:
                    // Double video name.
                    filename = ExporterVideoDual.SuggestFilename(player1, player2);
                    break;
            }

            return filename;
        }
    }
}
