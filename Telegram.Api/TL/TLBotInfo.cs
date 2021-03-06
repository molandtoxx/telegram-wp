﻿// 
// This is the source code of Telegram for Windows Phone v. 3.x.x.
// It is licensed under GNU GPL v. 2 or later.
// You should have received a copy of the license in this archive (see LICENSE).
// 
// Copyright Evgeny Nadymov, 2013-present.
// 
using System.IO;
using Telegram.Api.Extensions;

namespace Telegram.Api.TL
{
    public abstract class TLBotInfoBase : TLObject { }

    public class TLBotInfoEmpty : TLBotInfoBase
    {
        public const uint Signature = TLConstructors.TLBotInfoEmpty;

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature));
        }

        public override TLObject FromStream(Stream input)
        {
            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
        }
    }

    public class TLBotInfo49 : TLBotInfo
    {
        public new const uint Signature = TLConstructors.TLBotInfo49;

        public override TLInt Version
        {
            get { return new TLInt(0); }
            set { }
        }

        public override TLString ShareText
        {
            get { return TLString.Empty; }
            set { }
        }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);
            Description = GetObject<TLString>(bytes, ref position);
            Commands = GetObject<TLVector<TLBotCommand>>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                UserId.ToBytes(),
                Description.ToBytes(),
                Commands.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);
            Description = GetObject<TLString>(input);
            Commands = GetObject<TLVector<TLBotCommand>>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(UserId.ToBytes());
            output.Write(Description.ToBytes());
            output.Write(Commands.ToBytes());
        }
    }

    public class TLBotInfo : TLBotInfoBase
    {
        public const uint Signature = TLConstructors.TLBotInfo;

        public TLInt UserId { get; set; }

        public virtual TLInt Version { get; set; }

        public virtual TLString ShareText { get; set; }

        public TLString Description { get; set; }

        public TLVector<TLBotCommand> Commands { get; set; }

        public override TLObject FromBytes(byte[] bytes, ref int position)
        {
            bytes.ThrowExceptionIfIncorrect(ref position, Signature);

            UserId = GetObject<TLInt>(bytes, ref position);
            Version = GetObject<TLInt>(bytes, ref position);
            ShareText = GetObject<TLString>(bytes, ref position);
            Description = GetObject<TLString>(bytes, ref position);
            Commands = GetObject<TLVector<TLBotCommand>>(bytes, ref position);

            return this;
        }

        public override byte[] ToBytes()
        {
            return TLUtils.Combine(
                TLUtils.SignatureToBytes(Signature),
                UserId.ToBytes(),
                Version.ToBytes(),
                ShareText.ToBytes(),
                Description.ToBytes(),
                Commands.ToBytes());
        }

        public override TLObject FromStream(Stream input)
        {
            UserId = GetObject<TLInt>(input);
            Version = GetObject<TLInt>(input);
            ShareText = GetObject<TLString>(input);
            Description = GetObject<TLString>(input);
            Commands = GetObject<TLVector<TLBotCommand>>(input);

            return this;
        }

        public override void ToStream(Stream output)
        {
            output.Write(TLUtils.SignatureToBytes(Signature));
            output.Write(UserId.ToBytes());
            output.Write(Version.ToBytes());
            output.Write(ShareText.ToBytes());
            output.Write(Description.ToBytes());
            output.Write(Commands.ToBytes());
        }
    }
}
