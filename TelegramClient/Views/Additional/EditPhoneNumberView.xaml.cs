﻿// 
// This is the source code of Telegram for Windows Phone v. 3.x.x.
// It is licensed under GNU GPL v. 2 or later.
// You should have received a copy of the license in this archive (see LICENSE).
// 
// Copyright Evgeny Nadymov, 2013-present.
// 
using Microsoft.Phone.Shell;

namespace TelegramClient.Views.Additional
{
    public partial class EditPhoneNumberView
    {
        public EditPhoneNumberView()
        {
            InitializeComponent();

            OptimizeFullHD();
        }

        private void OptimizeFullHD()
        {
            var appBar = new ApplicationBar();
            var appBarDefaultSize = appBar.DefaultSize;

            ChangePhoneNumberPanel.Height = appBarDefaultSize;
        }
    }
}