﻿namespace Cedar.Internal
{
    public static class InterlockedBooleanExtensions
    {
        public static bool EnsureCalledOnce(this InterlockedBoolean interlockedBoolean)
        {
            return interlockedBoolean.CompareExchange(true, false);
        }
    }
}