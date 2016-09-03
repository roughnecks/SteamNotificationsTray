﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Net;
using System.Threading.Tasks;

namespace SteamNotificationsTray
{
    class TrayAppContext : ApplicationContext
    {
        NotifyIcon mainIcon = new NotifyIcon();
        NotifyIcon countIcon = new NotifyIcon();
        ContextMenu appContextMenu;
        ContextMenuStrip notificationsContextMenu;
        Timer refreshTimer = new Timer();
        NotificationsClient client = new NotificationsClient();
        bool newNotifAcknowledged;
        bool hasNotifications;

        public TrayAppContext()
        {
            appContextMenu = new ContextMenu(new MenuItem[] {
                new MenuItem("Refresh now", (sender, e) => {
                    updateNotifications();
                }),
                new MenuItem("Exit", (sender, e) => {
                    Application.Exit();
                })
            });

            refreshTimer.Interval = Properties.Settings.Default.RefreshInterval;
            refreshTimer.Tick += refreshTimer_Tick;

            // Must do this true/false charade to get context menus associated for some reason
            countIcon.ContextMenu = appContextMenu;
            countIcon.Visible = true;
            countIcon.Visible = false;
            mainIcon.ContextMenu = appContextMenu;
            mainIcon.Icon = IconUtils.CreateIconWithBackground(Properties.Resources.NotificationDefault, Properties.Settings.Default.InboxNoneColor, SystemInformation.SmallIconSize);
            mainIcon.Visible = true;
            mainIcon.Visible = false;

            mainIcon.Click += notifyIcon_Click;
            countIcon.Click += notifyIcon_Click;

            // If no cookies available, show login form
            //CredentialStore.ClearCredentials();
            if (!CredentialStore.CredentialsAvailable())
            {
                LoginForm loginForm = new LoginForm();
                loginForm.FormClosed += loginForm_FormClosed;
                MainForm = loginForm;
                loginForm.Show();
            }
            else
            {
                finishSetup();
            }
        }

        void loginForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            Form form = sender as Form;
            MainForm = null;
            if (form.DialogResult == DialogResult.OK)
                finishSetup(); // Login OK, start the rest of the app
            else
                Application.Exit(); // Login canceled, exit
        }
        
        void finishSetup()
        {
            // Set up cookies
            CookieContainer cookies = CredentialStore.GetCommunityCookies();
            client.SetCookies(cookies);

            // Set up timer and fire
            refreshTimer.Start();
            updateNotifications();
        }

        async void refreshTimer_Tick(object sender, EventArgs e)
        {
            await updateNotifications();
        }

        async Task updateNotifications()
        {
            try
            {
                NotificationCounts counts = await client.PollNotificationCountsAsync();
                if (counts != null)
                {
                    if (counts.TotalNotifications == 0)
                    {
                        hasNotifications = false;
                        countIcon.Visible = false;
                        ReplaceNotifyIcon(mainIcon, IconUtils.CreateIconWithBackground(Properties.Resources.NotificationDefault, Properties.Settings.Default.InboxNoneColor, SystemInformation.SmallIconSize));
                    }
                    else
                    {
                        hasNotifications = true;
                        NotificationCounts oldCounts = client.PrevCounts;
                        Color newColor;
                        if (oldCounts == null)
                        {
                            newNotifAcknowledged = true;
                            newColor = Properties.Settings.Default.InboxAvailableColor;
                        }
                        else
                        {
                            if (counts.TotalNotifications > oldCounts.TotalNotifications)
                            {
                                newNotifAcknowledged = false;
                                newColor = Properties.Settings.Default.InboxNewColor;
                            }
                            else if (counts.TotalNotifications == oldCounts.TotalNotifications)
                            {
                                newColor = newNotifAcknowledged ? Properties.Settings.Default.InboxAvailableColor : Properties.Settings.Default.InboxNewColor;
                            }
                            else
                            {
                                newNotifAcknowledged = true;
                                newColor = Properties.Settings.Default.InboxAvailableColor;
                            }                            
                        }

                        ReplaceNotifyIcon(mainIcon, IconUtils.CreateIconWithBackground(Properties.Resources.NotificationActive, newColor, SystemInformation.SmallIconSize));
                        
                        // 7 point for 3 digits
                        // 8 point for 2 digits
                        // 9 point for 1 digit
                        string text = counts.TotalNotifications.ToString();
                        ReplaceNotifyIcon(countIcon, IconUtils.CreateIconWithText(text, new Font("Arial", 10 - text.Length, FontStyle.Regular, GraphicsUnit.Point), newColor, SystemInformation.SmallIconSize));

                        if (!countIcon.Visible)
                        {
                            // Hide main icon first, then show in this order so the count is on the left
                            mainIcon.Visible = false;
                            countIcon.Visible = true;
                            mainIcon.Visible = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Should handle 401 exceptions by asking to log in again
                //MessageBox.Show(ex.ToString());
            }
        }

        void notifyIcon_Click(object sender, EventArgs e)
        {
            if (hasNotifications)
            {
                // Make icon normal colored
                newNotifAcknowledged = true;
                ReplaceNotifyIcon(mainIcon, IconUtils.CreateIconWithBackground(Properties.Resources.NotificationActive, Properties.Settings.Default.InboxAvailableColor, SystemInformation.SmallIconSize));
                string text = client.CurrentCounts.TotalNotifications.ToString();
                ReplaceNotifyIcon(countIcon, IconUtils.CreateIconWithText(text, new Font("Arial", 10 - text.Length, FontStyle.Regular, GraphicsUnit.Point), Properties.Settings.Default.InboxAvailableColor, SystemInformation.SmallIconSize));
            }
        }
        
        protected override void ExitThreadCore()
        {
            mainIcon.Visible = false;
            countIcon.Visible = false;
            base.ExitThreadCore();
        }

        static void ReplaceNotifyIcon(NotifyIcon notify, Icon newIcon)
        {
            if (notify.Icon != null) DestroyIcon(notify.Icon.Handle);
            notify.Icon = newIcon;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        extern static bool DestroyIcon(IntPtr handle);
    }
}
