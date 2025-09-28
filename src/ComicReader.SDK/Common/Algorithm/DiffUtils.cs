// Copyright (c) aicd0. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

namespace ComicReader.SDK.Common.Algorithm;

public static class DiffUtils
{
    public static void UpdateCollection<T>(Collection<T> fromCollection, IReadOnlyList<T> toCollection, Func<T, T, bool> comparer)
    {
        List<Modification> modifications = CalculateModifications(fromCollection, toCollection, comparer, false);
        UpdateCollection(fromCollection, toCollection, modifications);
    }

    public static void UpdateCollection<T>(Collection<T> fromCollection, IReadOnlyList<T> toCollection, Func<T, T, bool> comparer, Action<T, T> updater, bool disableME = false)
    {
        Collection<bool> fromCollectionFlags = [];
        for (int i = 0; i < fromCollection.Count; ++i)
        {
            fromCollectionFlags.Add(true);
        }

        Collection<bool> toCollectionFlags = [];
        for (int i = 0; i < toCollection.Count; ++i)
        {
            toCollectionFlags.Add(false);
        }

        List<Modification> modifications = CalculateModifications(fromCollection, toCollection, comparer, disableME);
        UpdateCollection(fromCollection, toCollection, modifications);
        UpdateCollection(fromCollectionFlags, toCollectionFlags, modifications);
        for (int i = 0; i < fromCollection.Count; ++i)
        {
            if (fromCollectionFlags[i])
            {
                updater(fromCollection[i], toCollection[i]);
            }
        }
    }

    public static void UpdateCollectionUsingME<T>(Collection<T> fromCollection, IReadOnlyList<T> toCollection, Func<T, T, bool> comparer)
    {
        List<Modification> modifications = UpdateCollectionUsingMinimumEditing(fromCollection, toCollection, comparer);
        UpdateCollection(fromCollection, toCollection, modifications);
    }

    public static void UpdateCollectionUsingAF<T>(Collection<T> fromCollection, IReadOnlyList<T> toCollection, Func<T, T, bool> comparer)
    {
        List<Modification> modifications = UpdateCollectionUsingAddFirstMatch(fromCollection, toCollection, comparer);
        UpdateCollection(fromCollection, toCollection, modifications);
    }

    public static void UpdateCollectionUsingDF<T>(Collection<T> fromCollection, IReadOnlyList<T> toCollection, Func<T, T, bool> comparer)
    {
        List<Modification> modifications = UpdateCollectionUsingDeleteFirstMatch(fromCollection, toCollection, comparer);
        UpdateCollection(fromCollection, toCollection, modifications);
    }

    private static List<Modification> CalculateModifications<T>(Collection<T> fromCollection, IReadOnlyList<T> toCollection, Func<T, T, bool> comparer, bool disableME)
    {
        List<Modification> modifications;
        if (!disableME && fromCollection.Count * toCollection.Count <= 65536)
        {
            modifications = UpdateCollectionUsingMinimumEditing(fromCollection, toCollection, comparer);
        }
        else
        {
            List<Modification> deleteFirstModifications = UpdateCollectionUsingDeleteFirstMatch(fromCollection, toCollection, comparer);
            List<Modification> addFirstModifications = UpdateCollectionUsingAddFirstMatch(fromCollection, toCollection, comparer);
            modifications = deleteFirstModifications.Count <= addFirstModifications.Count ? deleteFirstModifications : addFirstModifications;
        }

        return modifications;
    }

    private static void UpdateCollection<T>(Collection<T> fromCollection, IReadOnlyList<T> toCollection, List<Modification> modifications)
    {
        foreach (Modification modification in modifications)
        {
            switch (modification.Type)
            {
                case ModificationType.Delete:
                    fromCollection.RemoveAt(modification.FromIndex);
                    break;
                case ModificationType.Add:
                    fromCollection.Insert(modification.FromIndex, toCollection[modification.ToIndex]);
                    break;
                default:
                    throw new InvalidOperationException("Unexpected modification type.");
            }
        }
    }

    private static List<Modification> UpdateCollectionUsingMinimumEditing<T>(IReadOnlyList<T> fromCollection, IReadOnlyList<T> toCollection, Func<T, T, bool> comparer)
    {
        // DP Problem: Edit Distance
        // Initialize cache
        var modificationTable = new List<List<MEModification>>(fromCollection.Count + 1);
        for (int i = 0; i < fromCollection.Count + 1; i++)
        {
            var row = new List<MEModification>(toCollection.Count + 1);
            for (int j = 0; j < toCollection.Count + 1; j++)
            {
                row.Add(new MEModification());
            }
            modificationTable.Add(row);
        }
        for (int j = 1; j < modificationTable[0].Count; ++j)
        {
            MEModification mod = modificationTable[0][j];
            mod.Type = MEModificationType.Add;
            mod.MinSteps = j;
        }
        for (int i = 1; i < modificationTable.Count; ++i)
        {
            MEModification mod = modificationTable[i][0];
            mod.Type = MEModificationType.Delete;
            mod.MinSteps = i;
        }

        // Update cache
        for (int i = 1; i < modificationTable.Count; ++i)
        {
            List<MEModification> row = modificationTable[i];
            List<MEModification> lastRow = modificationTable[i - 1];
            for (int j = 1; j < row.Count; ++j)
            {
                bool equal = comparer(fromCollection[i - 1], toCollection[j - 1]);
                int leftTop = lastRow[j - 1].MinSteps;
                int left = row[j - 1].MinSteps;
                int top = lastRow[j].MinSteps;
                int skipStep = equal ? leftTop : leftTop + 2;
                int addStep = left + 1;
                int deleteStep = top + 1;
                int minStep = Math.Min(Math.Min(skipStep, addStep), deleteStep);
                row[j].MinSteps = minStep;
                if (minStep == skipStep)
                {
                    row[j].Type = equal ? MEModificationType.Skip : MEModificationType.Replace;
                }
                else if (minStep == addStep)
                {
                    row[j].Type = MEModificationType.Add;
                }
                else
                {
                    row[j].Type = MEModificationType.Delete;
                }
            }
        }

        // Backtracking for solution
        var solution = new LinkedList<Modification>();
        {
            LinkedListNode<Modification> head = solution.AddFirst(new Modification());
            LinkedListNode<Modification> tail = solution.AddLast(new Modification());
            int i = fromCollection.Count;
            int j = toCollection.Count;
            while (i > 0 || j > 0)
            {
                MEModificationType type = modificationTable[i][j].Type;
                switch (type)
                {
                    case MEModificationType.Skip:
                        --i;
                        --j;
                        break;
                    case MEModificationType.Replace:
                        --i;
                        --j;
                        head = solution.AddAfter(head, new Modification
                        {
                            FromIndex = i,
                            Type = ModificationType.Delete
                        });
                        tail = solution.AddBefore(tail, new Modification
                        {
                            FromIndex = j,
                            ToIndex = j,
                            Type = ModificationType.Add
                        });
                        break;
                    case MEModificationType.Delete:
                        --i;
                        head = solution.AddAfter(head, new Modification
                        {
                            FromIndex = i,
                            Type = ModificationType.Delete
                        });
                        break;
                    case MEModificationType.Add:
                        --j;
                        tail = solution.AddBefore(tail, new Modification
                        {
                            FromIndex = j,
                            ToIndex = j,
                            Type = ModificationType.Add
                        });
                        break;
                    default:
                        throw new InvalidOperationException("Unexpected modification type.");
                }
            }
            solution.RemoveFirst();
            solution.RemoveLast();
        }
        return [.. solution];
    }

    private static List<Modification> UpdateCollectionUsingDeleteFirstMatch<T>(IReadOnlyList<T> fromCollection, IReadOnlyList<T> toCollection, Func<T, T, bool> comparer)
    {
        List<Modification> modifications = [];
        int i = 0, j = 0, offset = 0;
        while (true)
        {
            if (i < fromCollection.Count)
            {
                if (j < toCollection.Count && comparer(fromCollection[i], toCollection[j]))
                {
                    ++j;
                }
                else
                {
                    modifications.Add(new Modification
                    {
                        FromIndex = i + offset,
                        Type = ModificationType.Delete
                    });
                    --offset;
                }
                ++i;
            }
            else if (j < toCollection.Count)
            {
                modifications.Add(new Modification
                {
                    FromIndex = i + offset,
                    ToIndex = j,
                    Type = ModificationType.Add
                });
                ++offset;
                ++j;
            }
            else
            {
                break;
            }
        }
        return modifications;
    }

    private static List<Modification> UpdateCollectionUsingAddFirstMatch<T>(IReadOnlyList<T> fromCollection, IReadOnlyList<T> toCollection, Func<T, T, bool> comparer)
    {
        List<Modification> modifications = [];
        int i = 0, j = 0, offset = 0;
        while (true)
        {
            if (j < toCollection.Count)
            {
                if (i < fromCollection.Count && comparer(fromCollection[i], toCollection[j]))
                {
                    ++i;
                }
                else
                {
                    modifications.Add(new Modification
                    {
                        FromIndex = i + offset,
                        ToIndex = j,
                        Type = ModificationType.Add
                    });
                    ++offset;
                }
                ++j;
            }
            else if (i < fromCollection.Count)
            {
                modifications.Add(new Modification
                {
                    FromIndex = i + offset,
                    Type = ModificationType.Delete
                });
                --offset;
                ++i;
            }
            else
            {
                break;
            }
        }
        return modifications;
    }

    private enum MEModificationType
    {
        Unset,
        Skip,
        Replace,
        Delete,
        Add
    }

    private class MEModification
    {
        public MEModificationType Type = MEModificationType.Unset;
        public int MinSteps = 0;
    }

    private enum ModificationType
    {
        Delete,
        Add
    }

    private class Modification
    {
        public int ToIndex;
        public int FromIndex;
        public ModificationType Type;
    }
}
