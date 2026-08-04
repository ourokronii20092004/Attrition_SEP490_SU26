"use client";

import { useRef, useState } from "react";

/**
 * Inline edit that commits when you click away, so nobody loses a change by forgetting Save.
 *
 * The subtlety is Cancel. A blur fires before the click that caused it, so a naive
 * save-on-blur would persist the very edit the user was trying to discard. Both buttons mark
 * the next blur as intentional (`armButton`) and the blur handler stands down, leaving the
 * click to decide. Same guard stops Save from writing twice.
 *
 * Committing an unchanged value is skipped entirely — clicking in and straight back out
 * shouldn't cost a request.
 */
export function useInlineEdit({ initial, onCommit }: {
  initial: string;
  onCommit: (value: string) => Promise<void>;
}) {
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(initial);
  const [busy, setBusy] = useState(false);
  // Set by a button's mousedown, which lands before the blur it triggers.
  const buttonArmed = useRef(false);

  const dirty = value.trim() !== initial.trim();

  const start = () => {
    setValue(initial);
    setEditing(true);
  };

  const cancel = () => {
    buttonArmed.current = false;
    setValue(initial);
    setEditing(false);
  };

  const commit = async () => {
    buttonArmed.current = false;
    if (!dirty) {
      setEditing(false);
      return;
    }
    setBusy(true);
    try {
      await onCommit(value.trim());
      setEditing(false);
    } finally {
      setBusy(false);
    }
  };

  /** Put on the field: commits on click-away unless a button is handling it. */
  const onBlur = () => {
    if (buttonArmed.current) {
      buttonArmed.current = false;
      return;
    }
    void commit();
  };

  /** Put on Save and Cancel so their click, not the blur, decides the outcome. */
  const armButton = { onMouseDown: () => { buttonArmed.current = true; } };

  /** Escape discards. `submitOnEnter` suits single-line fields; multiline wants Ctrl/Cmd+Enter. */
  const onKeyDown = (submitOnEnter: boolean) => (e: React.KeyboardEvent) => {
    if (e.key === "Escape") {
      e.preventDefault();
      cancel();
      return;
    }
    if (e.key === "Enter" && (submitOnEnter ? !e.shiftKey : e.ctrlKey || e.metaKey)) {
      e.preventDefault();
      void commit();
    }
  };

  return { editing, value, setValue, busy, dirty, start, cancel, commit, onBlur, armButton, onKeyDown };
}
