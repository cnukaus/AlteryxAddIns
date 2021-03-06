using System;
using System.Xml;

using AlteryxRecordInfoNet;

using OmniBus.Framework.Interfaces;

namespace OmniBus.Framework
{
    /// <summary>
    ///     Output Helper Class
    /// </summary>
    internal sealed class OutputHelper : IDisposable, IOutputHelper
    {
        private readonly string _connectionName;

        private readonly IBaseEngine _hostEngine;

        private PluginOutputConnectionHelper _helper;

        private Lazy<Record> _lazyRecord;

        private double _percentage;

        private ulong _recordCount;

        private ulong _recordLength;

        /// <summary>
        ///     Initializes a new instance of the <see cref="OutputHelper" /> class.
        /// </summary>
        /// <param name="hostEngine">The host engine.</param>
        /// <param name="connectionName">Name of the outgoing connection.</param>
        public OutputHelper(IBaseEngine hostEngine, string connectionName)
        {
            this._hostEngine = hostEngine;
            this._connectionName = connectionName;

            this._helper = new PluginOutputConnectionHelper(this._hostEngine.NToolId, this._hostEngine.Engine);
        }

        /// <summary>
        ///     Gets the <see cref="RecordInfo" /> describing the Output records
        /// </summary>
        public RecordInfo RecordInfo { get; private set; }

        /// <summary>
        ///     Gets a reusable <see cref="Record" /> object
        /// </summary>
        public Record Record => this._lazyRecord?.Value;

        /// <summary>
        ///     Given a fieldName, gets the <see cref="FieldBase" /> for it
        /// </summary>
        /// <param name="fieldName">Name of field</param>
        /// <returns><see cref="FieldBase" /> representing the field</returns>
        public FieldBase this[string fieldName] => this.RecordInfo?.GetFieldByName(fieldName, false);

        /// <summary>
        ///     Dispose of the internal helper and release the reference
        /// </summary>
        public void Dispose()
        {
            if (this._helper != null)
            {
                this._helper.Dispose();
                this._helper = null;
            }
        }

        /// <summary>
        ///     Initializes the output stream.
        /// </summary>
        /// <param name="recordInfo">RecordInfo defining the fields and outputs of the connection.</param>
        /// <param name="sortConfig">Sort configuration to pass onto Alteryx.</param>
        /// <param name="oldConfig">XML configuration of the tool.</param>
        public void Init(RecordInfo recordInfo, XmlElement sortConfig = null, XmlElement oldConfig = null)
        {
            this.RecordInfo = recordInfo;
            this._lazyRecord = new Lazy<Record>(() => this.RecordInfo?.CreateRecord());

            this._recordCount = 0;
            this._recordLength = 0;

            this._helper?.Init(recordInfo, this._connectionName, sortConfig, oldConfig ?? this._hostEngine.XmlConfig);
            this._hostEngine.Engine.OutputMessage(
                this._hostEngine.NToolId,
                MessageStatus.STATUS_Info,
                $"Init called back on {this._connectionName}");
        }

        /// <summary>
        ///     Using <see cref="IOutputHelper.Record"/> push data to Alteryx
        /// </summary>
        /// <param name="data">Array of data to push</param>
        public void PushData(params object[] data)
        {
            void SetDateTimeField(object dataValue, Record recordObject, FieldBase fieldBaseObject, string format)
            {
                if (dataValue is DateTime dateTime)
                {
                    fieldBaseObject.SetFromString(recordObject, dateTime.ToString(format));
                }
                else if (dataValue is TimeSpan t)
                {
                    fieldBaseObject.SetFromString(recordObject, DateTime.Today.Add(t).ToString(format));
                }
                else
                {
                    fieldBaseObject.SetFromString(recordObject, dataValue.ToString());
                }
            }

            var record = this.Record;
            record.Reset();

            for (int i = 0; i < data.Length; i++)
            {
                var fieldBase = this.RecordInfo[i];
                if (data[i] == null)
                {
                    fieldBase.SetNull(record);
                    continue;
                }

                switch (fieldBase.FieldType)
                {
                    case FieldType.E_FT_Bool:
                        fieldBase.SetFromBool(record, (bool)data[i]);
                        break;
                    case FieldType.E_FT_Byte:
                    case FieldType.E_FT_Int16:
                    case FieldType.E_FT_Int32:
                        fieldBase.SetFromInt32(record, (ValueType)data[i]);
                        break;
                    case FieldType.E_FT_Int64:
                        fieldBase.SetFromInt64(record, (ValueType)data[i]);
                        break;
                    case FieldType.E_FT_Float:
                    case FieldType.E_FT_Double:
                        fieldBase.SetFromDouble(record, (ValueType)data[i]);
                        break;
                    case FieldType.E_FT_Date:
                        SetDateTimeField(data[i], record, fieldBase, "yyyy-MM-dd");
                        break;
                    case FieldType.E_FT_DateTime:
                        SetDateTimeField(data[i], record, fieldBase, "yyyy-MM-dd HH:mm:ss");
                        break;
                    case FieldType.E_FT_Time:
                        SetDateTimeField(data[i], record, fieldBase, "HH:mm:ss");
                        break;
                    default:
                        fieldBase.SetFromString(record, data[i].ToString());
                        break;
                }
            }

            this.Push(record, false, 0);
        }

        /// <summary>
        ///     Pushes a record to Alteryx to hand onto over tools.
        /// </summary>
        /// <param name="record">Record object to push to the stream.</param>
        /// <param name="close">Value indicating whether to close the connection after pushing the record.</param>
        /// <param name="updateCountMod">How often to update Row Count and Data</param>
        public void Push(Record record, bool close = false, ulong updateCountMod = 250)
        {
            this._helper?.PushRecord(record.GetRecord());

            this._recordCount++;
            this._recordLength += (ulong)((IntPtr)this.RecordInfo.GetRecordLen(record.GetRecord())).ToInt64();

            if (close)
            {
                this.Close();
            }
            else
            {
                if (updateCountMod > 0 && this._recordCount % updateCountMod == 0)
                {
                    this.PushCountAndSize();
                }
            }
        }

        /// <summary>
        ///     Update The Progress Of A Connection
        /// </summary>
        /// <param name="percentage">Percentage Progress from 0.0 to 1.0</param>
        /// <param name="setToolProgress">Set Tool Progress As Well</param>
        public void UpdateProgress(double percentage, bool setToolProgress = false)
        {
            if (Math.Abs(percentage - this._percentage) > 0.01)
            {
                this._percentage = percentage;
                this._helper?.UpdateProgress(percentage);

                if (setToolProgress)
                {
                    this._hostEngine.Engine.OutputToolProgress(this._hostEngine.NToolId, percentage);
                }
            }
        }

        /// <summary>
        ///     Tell Alteryx We Are Finished
        /// </summary>
        /// <param name="executionComplete">Tell Alteryx Tool Execution Is Complete</param>
        public void Close(bool executionComplete = false)
        {
            this._helper?.Close();
            this.PushCountAndSize(true);

            if (executionComplete)
            {
                this._hostEngine.ExecutionComplete();
            }

            this.RecordInfo = null;
            this._lazyRecord = null;
        }

        /// <summary>
        ///     Adds the connection.
        /// </summary>
        /// <param name="connection">The connection.</param>
        public void AddConnection(OutgoingConnection connection) => this._helper?.AddOutgoingConnection(connection);

        /// <summary>
        ///     Push Record Count and Size to Alteryx
        /// </summary>
        /// <param name="final">Tell Alterysx Is Funal Update</param>
        public void PushCountAndSize(bool final = false)
        {
            this._hostEngine.Engine.OutputMessage(
                this._hostEngine.NToolId,
                MessageStatus.STATUS_RecordCountAndSize,
                $"{this._connectionName}|{this._recordCount}|{this._recordLength}");
            this._helper?.OutputRecordCount(final);
        }
    }
}