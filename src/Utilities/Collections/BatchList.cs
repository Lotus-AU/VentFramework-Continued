using System;
using System.Collections.Generic;
using System.Data;
using Hazel;
using UnityEngine;
using VentLib.Networking;
using VentLib.Networking.Batches;
using VentLib.Networking.Helpers;
using VentLib.Networking.Interfaces;
using VentLib.Utilities.Optionals;

namespace VentLib.Utilities.Collections;

public class BatchList<T>: List<T>, IBatchSendable<BatchList<T>> where T: IRpcSendable<T>
{
    public BatchList()
    {
    }

    public BatchList(IEnumerable<T> collection): base(collection)
    {
    }

    private Optional<int> itemSize = Optional<int>.Null();

    public void SetItemSize(int bytes)
    {
        itemSize = Optional<int>.NonNull(bytes);
    }

    public BatchEnd Write(BatchWriter batchWriter)
    {
        batchWriter.Write(Count);
        if (Count == 0)
            return batchWriter.EndBatch();

        int safePacketLimit = NetworkRules.MaxPacketSize - 64;

        int index = 0;

        while (index < Count)
        {
            int batchSize = 0;
            int batchItemCount = 0;

            int startIndex = index;

            while (index < Count)
            {
                MessageWriter temp = MessageWriter.Get();
                this[index].Write(temp);
                int indexMessageSize = temp.Length;

                if (indexMessageSize > safePacketLimit)
                    throw new ConstraintException(
                        $"Single item exceeds max packet size ({indexMessageSize} bytes)");

                if (batchSize + indexMessageSize > safePacketLimit)
                    break;

                batchSize += indexMessageSize;
                batchItemCount++;
                index++;
                temp.Recycle();
            }

            batchWriter.Write(batchItemCount);

            for (int i = 0; i < batchItemCount; i++)
            {
                batchWriter.Write(this[startIndex + i]);
            }

            if (index < Count)
                batchWriter = batchWriter.NextBatch();
        }

        return batchWriter.EndBatch();
    }


    public BatchList<T> Read(BatchReader batchReader)
    {
        MessageReader reader = batchReader.GetNext();
        int total = reader.ReadInt32();
        if (total == 0) return this;

        Type itemType = typeof(T);
        while (batchReader.HasNext())
        {
            reader = batchReader.GetNext();
            int batchItemCount = reader.ReadInt32();
            for (int i = 0; i < batchItemCount; i++)
            {
                T item = reader.ReadDynamic(itemType);
                Add(item);
            }
        }
        return this;
    }
}