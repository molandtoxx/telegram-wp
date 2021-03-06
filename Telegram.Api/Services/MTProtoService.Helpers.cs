﻿// 
// This is the source code of Telegram for Windows Phone v. 3.x.x.
// It is licensed under GNU GPL v. 2 or later.
// You should have received a copy of the license in this archive (see LICENSE).
// 
// Copyright Evgeny Nadymov, 2013-present.
// 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Telegram.Logs;
#if WINDOWS_PHONE
using System.Globalization;
#endif
using Telegram.Api.Extensions;
using Telegram.Api.Helpers;
using Telegram.Api.TL;
using Telegram.Api.TL.Functions.Help;
using Telegram.Api.Transport;

namespace Telegram.Api.Services
{
    public partial class MTProtoService
    {
        private void PrintCaption(string caption)
        {
            TLUtils.WriteLine(" ");
            //TLUtils.WriteLine("------------------------");
            TLUtils.WriteLine(String.Format("-->>{0}", caption));
            TLUtils.WriteLine("------------------------");
        }

        public TLEncryptedTransportMessage GetEncryptedTransportMessage(byte[] authKey, TLLong salt, TLObject obj)
        {
            int sequenceNumber;
            TLLong messageId;
            lock (_activeTransportRoot)
            {
                sequenceNumber = 0;
                messageId = _activeTransport.GenerateMessageId(true);
            }
            var sessionId = TLLong.Random();
            var transportMessage = CreateTLTransportMessage(salt, sessionId, new TLInt(sequenceNumber), messageId, obj);
            var encryptedMessage = CreateTLEncryptedMessage(authKey, transportMessage);

            return encryptedMessage;
        }

        private readonly object _delayedNonInformativeItemsRoot = new object();

        private readonly List<DelayedItem> _delayedNonInformativeItems = new List<DelayedItem>();

        private void SendNonInformativeMessage<T>(string caption, TLObject obj, Action<T> callback, Action<TLRPCError> faultCallback = null) where T : TLObject
        {
            PrintCaption(caption);

            if (_activeTransport.AuthKey == null)
            {
                var delayedItem = new DelayedItem
                {
                    SendTime = DateTime.Now,
                    //SendBeforeTime = sendBeforeTime,
                    Caption = caption,
                    Object = obj,
                    Callback = t => callback((T)t),
                    AttemptFailed = null,
                    FaultCallback = faultCallback,
                };
                lock (_delayedNonInformativeItemsRoot)
                {
                    _delayedNonInformativeItems.Add(delayedItem);
                }
#if LOG_REGISTRATION
                TLUtils.WriteLog(string.Format("Add delayed item {0} sendTime={1}", delayedItem.Caption, delayedItem.SendTime.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)));
#endif

                return;
            }

            int sequenceNumber;
            TLLong messageId;
            lock (_activeTransportRoot)
            {
                sequenceNumber = _activeTransport.SequenceNumber * 2;
                messageId = _activeTransport.GenerateMessageId(true);
            }
            var authKey = _activeTransport.AuthKey;
            var salt = _activeTransport.Salt;
            var sessionId = _activeTransport.SessionId;
            var clientsTicksDelta = _activeTransport.ClientTicksDelta;
            var transportMessage = CreateTLTransportMessage(salt, sessionId, new TLInt(sequenceNumber), messageId, obj);
            var encryptedMessage = CreateTLEncryptedMessage(authKey, transportMessage);

            lock (_activeTransportRoot)
            {
                if (_activeTransport.Closed)
                {
                    _activeTransport = GetTransport(_activeTransport.Host, _activeTransport.Port, Type,
                        new TransportSettings
                        {
                            DcId = _activeTransport.DCId,
                            Secret = _activeTransport.Secret,
                            AuthKey = _activeTransport.AuthKey,
                            Salt = _activeTransport.Salt,
                            SessionId = _activeTransport.SessionId,
                            MessageIdDict = _activeTransport.MessageIdDict,
                            SequenceNumber = _activeTransport.SequenceNumber,
                            ClientTicksDelta = _activeTransport.ClientTicksDelta,
                            PacketReceivedHandler = OnPacketReceived
                        });
                }
            }

            HistoryItem historyItem = null;
            if (string.Equals(caption, "ping", StringComparison.OrdinalIgnoreCase)
                || string.Equals(caption, "ping_delay_disconnect", StringComparison.OrdinalIgnoreCase)
                || string.Equals(caption, "messages.container", StringComparison.OrdinalIgnoreCase))
            {
                //save items to history
                historyItem = new HistoryItem
                {
                    SendTime = DateTime.Now,
                    //SendBeforeTime = sendBeforeTime,
                    Caption = caption,
                    Object = obj,
                    Message = transportMessage,
                    Callback = t => callback((T)t),
                    AttemptFailed = null,
                    FaultCallback = faultCallback,
                    ClientTicksDelta = clientsTicksDelta,
                    Status = RequestStatus.Sent,
                };

                lock (_historyRoot)
                {
                    _history[historyItem.Hash] = historyItem;
                }
#if DEBUG
                NotifyOfPropertyChange(() => History);
#endif
            }

            //Debug.WriteLine(">>{0, -30} MsgId {1} SeqNo {2, -4} SessionId {3}", caption, transportMessage.MessageId.Value, transportMessage.SeqNo.Value, transportMessage.SessionId.Value);

            var captionString = string.Format("{0} {1}", caption, transportMessage.MessageId);

            SendPacketAsync(_activeTransport, captionString, encryptedMessage,
                result =>
                {
                    if (!result)
                    {
                        if (historyItem != null)
                        {
                            lock (_historyRoot)
                            {
                                _history.Remove(historyItem.Hash);
                            }
#if DEBUG
                            NotifyOfPropertyChange(() => History);
#endif
                        }
                        faultCallback.SafeInvoke(new TLRPCError(404) { Message = new TLString("FastCallback SocketError=" + result) });
                    }
                },
                error =>
                {
                    if (historyItem != null)
                    {
                        lock (_historyRoot)
                        {
                            _history.Remove(historyItem.Hash);
                        }
#if DEBUG
                        NotifyOfPropertyChange(() => History);
#endif
                    }
                    faultCallback.SafeInvoke(new TLRPCError(404)
                    {
#if WINDOWS_PHONE
                        SocketError = error.Error,
#endif
                        Exception = error.Exception
                    });
                });
        }

        private void SendPacketAsync(ITransport transport, string caption, TLObject data, Action<bool> callback, Action<TcpTransportResult> faultCallback = null)
        {
            if (_deviceInfo != null && _deviceInfo.IsBackground)
            {
                if (caption.Contains("account.updateStatus"))
                {

                }
                Log.SyncWrite("Background MTProto send " + caption);
            }

            transport.SendPacketAsync(
                caption,
                data.ToBytes(),
                callback,
                faultCallback);
        }

        private readonly object _historyRoot = new object();

        public void SendInformativeMessageInternal<T>(string caption, TLObject obj, Action<T> callback, Action<TLRPCError> faultCallback = null,
            int? maxAttempt = null, // to send delayed items
            Action<int> attemptFailed = null,
            Action fastCallback = null) // to send delayed items
            where T : TLObject
        {
            if (_activeTransport.AuthKey == null)
            {
                var delayedItem = new DelayedItem
                {
                    SendTime = DateTime.Now,
                    //SendBeforeTime = sendBeforeTime,
                    Caption = caption,
                    Object = obj,
                    Callback = t => callback((T)t),
                    AttemptFailed = attemptFailed,
                    FaultCallback = faultCallback,
                    MaxAttempt = maxAttempt
                };
                lock (_delayedItemsRoot)
                {
                    _delayedItems.Add(delayedItem);
                }
#if LOG_REGISTRATION
                TLUtils.WriteLog(string.Format("Add delayed item {0} sendTime={1}", delayedItem.Caption, delayedItem.SendTime.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)));
#endif

                return;
            }

            lock (_activeTransportRoot)
            {
                if (_activeTransport.Closed)
                {
                    _activeTransport = GetTransport(_activeTransport.Host, _activeTransport.Port, Type,
                        new TransportSettings
                        {
                            DcId = _activeTransport.DCId,
                            Secret = _activeTransport.Secret,
                            AuthKey = _activeTransport.AuthKey,
                            Salt = _activeTransport.Salt,
                            SessionId = _activeTransport.SessionId,
                            MessageIdDict = _activeTransport.MessageIdDict,
                            SequenceNumber = _activeTransport.SequenceNumber,
                            ClientTicksDelta = _activeTransport.ClientTicksDelta,
                            PacketReceivedHandler = OnPacketReceived
                        });
                }
            }

            PrintCaption(caption);

            TLObject data;
            lock (_activeTransportRoot)
            {
                if (!_activeTransport.Initiated || caption == "auth.sendCode")
                {
                    var initConnection = new TLInitConnection78
                    {
                        Flags = new TLInt(0),
                        AppId = new TLInt(Constants.ApiId),
                        AppVersion = new TLString(_deviceInfo.AppVersion),
                        Data = obj,
                        DeviceModel = new TLString(_deviceInfo.Model),
                        LangCode = new TLString(Utils.CurrentUICulture()),
                        SystemLangCode = new TLString(Utils.CurrentUICulture()),
                        LangPack = TLString.Empty,
                        SystemVersion = new TLString(_deviceInfo.SystemVersion),
                        Proxy = TLUtils.GetInputProxy(_transportService.GetProxyConfig())
                    };

                    //Execute.ShowDebugMessage("initConnection dc_id=" + _activeTransport.DCId);

                    SaveInitConnectionAsync(initConnection);

                    var withLayerN = new TLInvokeWithLayerN { Data = initConnection };
                    data = withLayerN;
                    _activeTransport.Initiated = true;
                }
                else
                {
                    data = obj;
                }
            }

            int sequenceNumber;
            TLLong messageId;
            lock (_activeTransportRoot)
            {
                sequenceNumber = _activeTransport.SequenceNumber * 2 + 1;
                _activeTransport.SequenceNumber++;
                messageId = _activeTransport.GenerateMessageId(true);
            }
            var authKey = _activeTransport.AuthKey;
            var salt = _activeTransport.Salt;
            var sessionId = _activeTransport.SessionId;
            var clientsTicksDelta = _activeTransport.ClientTicksDelta;
            var transportMessage = CreateTLTransportMessage(salt, sessionId, new TLInt(sequenceNumber), messageId, data);
            var encryptedMessage = CreateTLEncryptedMessage(authKey, transportMessage);

            //save items to history
            var historyItem = new HistoryItem
            {
                SendTime = DateTime.Now,
                //SendBeforeTime = sendBeforeTime,
                Caption = caption,
                Object = obj,
                Message = transportMessage,
                Callback = t => callback((T)t),
                AttemptFailed = attemptFailed,
                FaultCallback = faultCallback,
                ClientTicksDelta = clientsTicksDelta,
                Status = RequestStatus.Sent,
            };

            lock (_historyRoot)
            {
#if DEBUG
                HistoryItem existingItem;
                if (_history.TryGetValue(historyItem.Hash, out existingItem))
                {
                    Execute.ShowDebugMessage(string.Format("Duplicated history item hash={0} existing={1} new={2}", historyItem.Hash, existingItem.Caption, historyItem.Caption));
                }
                _history[historyItem.Hash] = historyItem;
#else
                _history[historyItem.Hash] = historyItem;
#endif
            }

#if DEBUG
            ITransport transport;
            lock (_activeTransportRoot)
            {
                transport = _activeTransport;
            }
            var transportId = transport.Id;
            var lastReceiveTime = transport.LastReceiveTime;
            int historyCount;
            string historyDescription;
            lock (_historyRoot)
            {
                historyCount = _history.Count;
                historyDescription = string.Join("\n", _history.Values.Select(x => x.Caption + " " + x.Hash));
            }
            var currentPacketLength = transport.PacketLength;
            var lastPacketLength = transport.LastPacketLength;

            RaiseTransportChecked(new TransportCheckedEventArgs
            {
                TransportId = transportId,
                SessionId = sessionId,
                AuthKey = authKey,
                LastReceiveTime = lastReceiveTime,
                HistoryCount = historyCount,
                HistoryDescription = historyDescription,
                NextPacketLength = currentPacketLength,
                LastPacketLength = lastPacketLength
            });
#endif

#if LOG_REGISTRATION
            TLUtils.WriteLog(string.Format("Add history item {0} sendTime={1}", historyItem.Caption, historyItem.SendTime.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture)));
#endif
#if DEBUG
            if (historyItem.Caption != "account.updateStatus") // to avoid deadlock on deactivation
            {
                NotifyOfPropertyChange(() => History);
            }
#endif

            //Debug.WriteLine(">>{0, -30} MsgId {1} SeqNo {2, -4} SessionId {3} ClientTicksDelta {4}", caption, transportMessage.MessageId.Value, transportMessage.SeqNo.Value, transportMessage.SessionId.Value, clientsTicksDelta);


            var captionString = string.Format("{0} {1} {2}", caption, transportMessage.SessionId, transportMessage.MessageId);
            SendPacketAsync(_activeTransport, captionString, encryptedMessage,
                result =>
                {
                    if (!result)
                    {
                        if (historyItem != null)
                        {
                            lock (_historyRoot)
                            {
                                _history.Remove(historyItem.Hash);
                            }
#if DEBUG
                            if (historyItem.Caption != "account.updateStatus") // to avoid deadlock on deactivation
                            {
                                NotifyOfPropertyChange(() => History);
                            }
#endif
                        }
                        faultCallback.SafeInvoke(new TLRPCError(404) { Message = new TLString("FastCallback SocketError=" + result) });
                    }
                },
                error =>
                {
                    if (historyItem != null)
                    {
                        lock (_historyRoot)
                        {
                            _history.Remove(historyItem.Hash);
                        }
#if DEBUG
                        if (historyItem.Caption != "account.updateStatus") // to avoid deadlock on deactivation
                        {
                            NotifyOfPropertyChange(() => History);
                        }
#endif
                    }
                    faultCallback.SafeInvoke(new TLRPCError(404)
                    {
#if WINDOWS_PHONE
                        SocketError = error.Error,
#endif
                        Exception = error.Exception
                    });
                });
        }

        private void SendInformativeMessage<T>(string caption, TLObject obj, Action<T> callback, Action<TLRPCError> faultCallback = null,
            int? maxAttempt = null,                 // to send delayed items
            Action<int> attemptFailed = null)       // to send delayed items
            where T : TLObject
        {
            Execute.BeginOnThreadPool(() =>
            {
                SendInformativeMessageInternal(caption, obj, callback, faultCallback, maxAttempt, attemptFailed);
            });
        }

        private void SendNonEncryptedMessage<T>(string caption, TLObject obj, Action<T> callback, Action<TLRPCError> faultCallback = null)
            where T : TLObject
        {
            PrintCaption(caption);
            TLLong messageId;
            lock (_activeTransportRoot)
            {
                messageId = _activeTransport.GenerateMessageId();
            }
            var message = CreateTLNonEncryptedMessage(messageId, obj);

            var historyItem = new HistoryItem
            {
                Caption = caption,
                Message = message,
                Callback = t => callback((T)t),
                FaultCallback = faultCallback,
                SendTime = DateTime.Now,
                Status = RequestStatus.Sent
            };

            var guid = message.MessageId;
            lock (_activeTransportRoot)
            {
                if (_activeTransport.Closed)
                {
                    _activeTransport = GetTransport(_activeTransport.Host, _activeTransport.Port, Type,
                        new TransportSettings
                        {
                            DcId = _activeTransport.DCId,
                            Secret = _activeTransport.Secret,
                            AuthKey = _activeTransport.AuthKey,
                            Salt = _activeTransport.Salt,
                            SessionId = _activeTransport.SessionId,
                            MessageIdDict = _activeTransport.MessageIdDict,
                            SequenceNumber = _activeTransport.SequenceNumber,
                            ClientTicksDelta = _activeTransport.ClientTicksDelta,
                            PacketReceivedHandler = OnPacketReceived
                        });
                }
            }

            var activeTransport = _activeTransport; // до вызова callback _activeTransport может уже поменяться

            // Сначала создаем или получаем транспорт, а потом добавляем в его историю.
            // Если сначала добавить в историю транспорта, то потом можем получить новый и не найдем запрос
            _activeTransport.EnqueueNonEncryptedItem(historyItem);

            var bytes = message.ToBytes();
#if LOG_REGISTRATION
            TLUtils.WriteLog(string.Format("SendPacketAsync {0} [{1}](data length={2})", _activeTransport.Id, caption, bytes.Length));
#endif
            var captionString = string.Format("{0} {1}", caption, guid);
            SendPacketAsync(_activeTransport, captionString, message,
                socketError =>
                {
#if LOG_REGISTRATION
                    TLUtils.WriteLog(string.Format("SendPacketAsync fastCallback {0} [{1}] socketError={2}", activeTransport.Id, caption, socketError));
#endif
                    if (!socketError)
                    {
                        var result = activeTransport.RemoveNonEncryptedItem(historyItem);

                        if (result)
                        {
                            faultCallback.SafeInvoke(new TLRPCError { Code = new TLInt(404), Message = new TLString("FastCallback SocketError=" + socketError) });
                        }
                    }
                },
                error =>
                {
#if LOG_REGISTRATION
                    TLUtils.WriteLog(string.Format("SendPacketAsync error {0} [{1}] error={2}", activeTransport.Id, caption, error));
#endif
                    var result = activeTransport.RemoveNonEncryptedItem(historyItem);

                    // чтобы callback не вызвался два раза из CheckTimeouts и отсюда
                    if (result)
                    {
                        faultCallback.SafeInvoke(new TLRPCError { Code = new TLInt(404), Message = new TLString("FaltCallback") });
                    }
                });
        }

        private static TLEncryptedTransportMessage CreateTLEncryptedMessage(byte[] authKey, TLContainerTransportMessage containerTransportMessage)
        {
            var message = new TLEncryptedTransportMessage { Data = containerTransportMessage.ToBytes() };

            return message.Encrypt(authKey);
        }

        private TLTransportMessage CreateTLTransportMessage(TLLong salt, TLLong sessionId, TLInt seqNo, TLLong messageId, TLObject obj)
        {
            var message = new TLTransportMessage();
            message.Salt = salt;
            message.SessionId = sessionId;
            message.MessageId = messageId;
            message.SeqNo = seqNo;
            message.MessageData = obj;

            return message;
        }

        public static TLNonEncryptedMessage CreateTLNonEncryptedMessage(TLLong messageId, TLObject obj)
        {
            var message = new TLNonEncryptedMessage();
            message.AuthKeyId = new TLLong(0);
            message.MessageId = messageId;
            message.Data = obj;

            return message;
        }
    }
}
