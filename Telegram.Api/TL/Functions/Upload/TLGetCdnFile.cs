﻿// 
// This is the source code of Telegram for Windows Phone v. 3.x.x.
// It is licensed under GNU GPL v. 2 or later.
// You should have received a copy of the license in this archive (see LICENSE).
// 
// Copyright Evgeny Nadymov, 2013-present.
// 
namespace Telegram.Api.TL.Functions.Upload
{
    public class TLGetCdnFile : TLObject
    {
        public const uint Signature = 0x2000bcc3;

        public TLString FileToken { get; set; }

        public TLInt Offset { get; set; }

        public TLInt Limit { get; set; }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                FileToken.ToBytes(),
                Offset.ToBytes(),
                Limit.ToBytes());
        }
    }
}
