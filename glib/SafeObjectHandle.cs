// SafeObjectHandle.cs - SafeHandle implementation for GObject
//
// Authors: Marius Ungureanu <maungu@microsoft.com>
//
// Copyright (c) 2017-2017 Microsoft, Inc.
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of version 2 of the Lesser GNU General
// Public License as published by the Free Software Foundation.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this program; if not, write to the
// Free Software Foundation, Inc., 59 Temple Place - Suite 330,
// Boston, MA 02111-1307, USA.

using System;
using System.Collections.Generic;
using Microsoft.Win32.SafeHandles;

namespace GLib
{
	public class SafeObjectHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		public static SafeObjectHandle Zero = new SafeObjectHandle ();

		internal ToggleRef tref;

		SafeObjectHandle () : base (false)
		{
		}

		protected SafeObjectHandle (IntPtr handle) : base (true)
		{
			SetHandle (handle);
		}

		static Func<IntPtr, SafeObjectHandle> InternalCreateHandle;

		static readonly Dictionary<IntPtr, ToggleRef> Objects = new Dictionary<IntPtr, ToggleRef> (IntPtrEqualityComparer.Instance);

		internal static SafeObjectHandle Create (Object obj, IntPtr handle)
		{
			if (handle == IntPtr.Zero)
				return Zero;

			SafeObjectHandle safeHandle;
			if (InternalCreateHandle != null)
				safeHandle = InternalCreateHandle.Invoke (handle);
			else
				safeHandle = new SafeObjectHandle (handle);
			safeHandle.tref = new ToggleRef (obj, handle);

			lock (Objects)
				Objects [handle] = safeHandle.tref;

			return safeHandle;
		}

		internal static bool TryGetObject (IntPtr o, out Object obj)
		{
			bool ret;
			ToggleRef tr;

			lock (Objects)
				ret = Objects.TryGetValue (o, out tr);

			obj = ret ? tr.Target : null;
			return obj != null;
		}

		static List<ToggleRef> PendingDestroys = new List<ToggleRef> ();
		static readonly object lockObject = new object ();
		static bool idle_queued;

		protected override bool ReleaseHandle ()
		{
			lock (Objects)
				Objects.Remove (handle);

			lock (lockObject) {
				if (tref != null) {
					PendingDestroys.Add (tref);
					if (!idle_queued) {
						Timeout.Add (50, PerformQueuedUnrefs);
						idle_queued = true;
					}
				}
			}
			return true;
		}

		static bool PerformQueuedUnrefs ()
		{
			List<ToggleRef> references;

			lock (lockObject) {
				references = PendingDestroys;
				PendingDestroys = new List<ToggleRef>();
				idle_queued = false;
			}

			foreach (ToggleRef r in references)
				r.Free();

			return false;
		}
	}
}
