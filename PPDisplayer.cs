﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OsuRTDataProvider.Listen;
using RealTimePPDisplayer.View;
using OsuRTDataProvider.Mods;
using RealTimePPDisplayer.Beatmap;
using System.Threading;
using static OsuRTDataProvider.Listen.OsuListenerManager;
using System.IO;
using System.Windows.Media;
using System.Windows;

namespace RealTimePPDisplayer
{
    class PPDisplayer
    {
        private OsuListenerManager m_listener_manager;

        private PPWindow m_win;
        private Thread m_pp_window_thread;

        private BeatmapReader m_beatmap_reader;
        private ModsInfo m_cur_mods = new ModsInfo();

        private OsuStatus m_status;

        private int m_combo = 0;
        private int m_max_combo = 0;
        private int m_n300 = 0;
        private int m_n100 = 0;
        private int m_n50 = 0;
        private int m_nmiss = 0;
        private int m_time = 0;

        string _filename = Path.GetFileNameWithoutExtension(Setting.TextOutputPath);
        string _ext = Path.GetExtension(Setting.TextOutputPath);

        public PPDisplayer(OsuListenerManager mamger,int? id)
        {
            m_listener_manager = mamger;

            m_listener_manager.OnModsChanged += (mods) => m_cur_mods = mods;
            m_listener_manager.On300HitChanged += c => m_n300 = c;
            m_listener_manager.On100HitChanged += c => m_n100 = c;
            m_listener_manager.On50HitChanged += c => m_n50 = c;
            m_listener_manager.OnMissHitChanged += c => m_nmiss = c;
            m_listener_manager.OnStatusChanged += (last, cur) =>
            {
                m_status = cur;
                if (cur == OsuStatus.Listening)//Reset(Change Song)
                {
                    m_max_combo = 0;
                    m_n100 = 0;
                    m_n50 = 0;
                    m_nmiss = 0;
                    if (Setting.UseText)
                    {
                        string str = "";
                        if (Setting.DisplayHitObject)
                            str += "";
                        File.WriteAllText(Setting.TextOutputPath, str);
                    }
                    else
                    {
                        m_win.Dispatcher.Invoke(() =>
                        {
                            m_win.ClearPP();
                            m_win.hit_label.Content = "";
                        });
                    }
                }
            };

            m_listener_manager.OnComboChanged += (combo) =>
            {
                if (m_status != OsuStatus.Playing) return;
                m_combo = combo;
                m_max_combo = Math.Max(m_max_combo, m_combo);
            };

            m_listener_manager.OnBeatmapChanged += (beatmap) =>
            {
                if (string.IsNullOrWhiteSpace(beatmap.Diff))
                {
                    m_beatmap_reader = null;
                    return;
                }

                string file = beatmap.LocationFile;
                if (string.IsNullOrWhiteSpace(file))
                {
                    Sync.Tools.IO.CurrentIO.Write("[RealTimePPDisplayer]No found .osu file");
                    m_beatmap_reader = null;
                    return;
                }
#if DEBUG
                Sync.Tools.IO.CurrentIO.Write($"[RealTimePPDisplayer]File:{file}");
#endif
                m_beatmap_reader = new BeatmapReader(file);
            };

            m_listener_manager.OnPlayingTimeChanged += time =>
            {
                if (time < 0) return;
                if (m_beatmap_reader == null) return;
                if (m_status != OsuStatus.Playing) return;
                if (m_cur_mods == ModsInfo.Mods.Unknown) return;

                if (m_time > time)//Reset
                {
                    m_max_combo = 0;
                    m_n100 = 0;
                    m_n50 = 0;
                    m_nmiss = 0;
                }

                int pos = m_beatmap_reader.GetPosition(time);
                
                double pp = PP.Oppai.get_ppv2(m_beatmap_reader.BeatmapRaw, (uint)pos, (uint)m_cur_mods.Mod, m_n50, m_n100, m_nmiss, m_max_combo);

                if (pp > 100000.0) pp = 0.0;

                if (Setting.UseText)
                {
                    string str = $"{pp:F2}pp";
                    if (Setting.DisplayHitObject)
                        str += $"\n{m_n100}x100 {m_n50}x50 {m_nmiss}xMiss";

                    File.WriteAllText($"{_filename}-0{_ext}", str);
                }
                else
                {
                    m_win?.Dispatcher.Invoke(() =>
                    {
                        m_win.PP = pp;
                        m_win.hit_label.Content = $"{m_n100}x100 {m_n50}x50 {m_nmiss}xMiss";
                    });
                }
                m_time = time;
            };

            if (!Setting.UseText)
            {
                m_pp_window_thread = new Thread(()=>ShowPPWindow(id));
                m_pp_window_thread.SetApartmentState(ApartmentState.STA);
                m_pp_window_thread.Start();
            }
        }

        private void ShowPPWindow(int? id)
        {
            m_win = new PPWindow(Setting.SmoothTime,Setting.FPS);
            m_win.Width = Setting.WindowWidth;
            m_win.Height = Setting.WindowHeight;

            if(id!=null)
                m_win.Title += $"{id}";

            m_win.client_id.Content = id?.ToString() ?? "";
            m_win.WindowStartupLocation = System.Windows.WindowStartupLocation.Manual;
            m_win.Left = SystemParameters.PrimaryScreenWidth - m_win.Width - 50;
            m_win.Top = 0; 

            m_win.SizeChanged += (o, e) =>
            {
                Setting.WindowHeight = (int)e.NewSize.Height;
                Setting.WindowWidth = (int)e.NewSize.Width;
            };

            if (!Setting.DisplayHitObject)
                m_win.hit_label.Visibility = System.Windows.Visibility.Hidden;

            m_win.pp_label.Foreground = new SolidColorBrush()
            {
                Color = Setting.PPFontColor
            };
            m_win.pp_label.FontSize = Setting.PPFontSize;

            m_win.hit_label.Foreground = new SolidColorBrush()
            {
                Color = Setting.HitObjectFontColor
            };
            m_win.hit_label.FontSize = Setting.HitObjectFontSize;

            m_win.Background = new SolidColorBrush()
            {
                Color = Setting.BackgroundColor
            };

            m_win.ShowDialog();
        }
    }
}
