import { ChildProcess } from 'child_process';

interface PendingRequest {
    resolve: (result: unknown) => void;
    reject:  (error:  unknown) => void;
}

/**
 * Minimal JSON-RPC 2.0 transport over a child process's stdio.
 * Implements Content-Length framing without any external library.
 *
 * Only client-initiated requests are supported here.
 * Server-initiated notifications will be added in a later milestone.
 */
export class JsonRpcTransport {
    private buffer  = Buffer.alloc(0);
    private nextId  = 1;
    private readonly pending = new Map<number, PendingRequest>();
    private readonly trace:  boolean;
    private readonly log:    (msg: string) => void;

    constructor(
        private readonly proc: ChildProcess,
        options: { trace?: boolean; log?: (msg: string) => void } = {}
    ) {
        this.trace = options.trace ?? false;
        this.log   = options.log   ?? (() => undefined);

        proc.stdout!.on('data', (chunk: Buffer) => this.onData(chunk));

        proc.stderr!.on('data', (chunk: Buffer) => {
            this.log(`[server] ${chunk.toString('utf8').trimEnd()}`);
        });

        proc.on('error', (err: Error) => {
            this.log(`[transport error] ${err.message}`);
            this.rejectAll(err);
        });

        proc.on('exit', (code: number | null) => {
            this.log(`[server exit] code=${code}`);
            if (this.pending.size > 0) {
                this.rejectAll(new Error(`Server exited with code ${code}`));
            }
        });
    }

    // ── Incoming data ────────────────────────────────────────────────────────

    private onData(chunk: Buffer): void {
        this.buffer = Buffer.concat([this.buffer, chunk]);
        let msg: unknown;
        while ((msg = this.tryConsumeMessage()) !== undefined) {
            this.dispatch(msg);
        }
    }

    /** Returns the next fully-buffered message, or undefined if more data is needed. */
    private tryConsumeMessage(): unknown | undefined {
        const sep = this.indexOfCrLfCrLf(this.buffer);
        if (sep === -1) return undefined;

        const headerSection = this.buffer.subarray(0, sep).toString('ascii');
        const match         = /content-length:\s*(\d+)/i.exec(headerSection);
        if (!match) {
            // Malformed framing — discard up to the separator and keep going.
            this.buffer = this.buffer.subarray(sep + 4);
            return undefined;
        }

        const contentLength = parseInt(match[1], 10);
        const totalLength   = sep + 4 + contentLength;
        if (this.buffer.length < totalLength) return undefined;

        const body = this.buffer.subarray(sep + 4, totalLength).toString('utf8');
        this.buffer = this.buffer.subarray(totalLength);

        if (this.trace) this.log(`← ${body}`);

        try {
            return JSON.parse(body);
        } catch {
            this.log(`[transport] JSON parse error in: ${body.slice(0, 200)}`);
            return undefined;
        }
    }

    private dispatch(msg: unknown): void {
        const m = msg as { id?: number; result?: unknown; error?: unknown };
        if (m.id !== undefined) {
            const pending = this.pending.get(m.id);
            if (pending) {
                this.pending.delete(m.id);
                if (m.error !== undefined) pending.reject(m.error);
                else pending.resolve(m.result ?? null);
            }
        }
        // Server-initiated notifications will be dispatched here in future milestones.
    }

    // ── Outgoing ─────────────────────────────────────────────────────────────

    request(method: string, params: unknown): Promise<unknown> {
        const id = this.nextId++;
        return new Promise<unknown>((resolve, reject) => {
            this.pending.set(id, { resolve, reject });
            this.write({ jsonrpc: '2.0', id, method, params });
        });
    }

    notify(method: string, params: unknown): void {
        this.write({ jsonrpc: '2.0', method, params });
    }

    private write(msg: object): void {
        const body   = Buffer.from(JSON.stringify(msg), 'utf8');
        const header = Buffer.from(`Content-Length: ${body.length}\r\n\r\n`, 'ascii');
        if (this.trace) this.log(`→ ${JSON.stringify(msg)}`);
        this.proc.stdin!.write(Buffer.concat([header, body]));
    }

    // ── Cleanup ──────────────────────────────────────────────────────────────

    dispose(): void {
        this.rejectAll(new Error('Transport disposed'));
        try { this.proc.stdin!.end(); } catch { /* already closed */ }
    }

    private rejectAll(err: Error): void {
        for (const { reject } of this.pending.values()) reject(err);
        this.pending.clear();
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private indexOfCrLfCrLf(buf: Buffer): number {
        for (let i = 0; i <= buf.length - 4; i++) {
            if (buf[i] === 0x0d && buf[i+1] === 0x0a &&
                buf[i+2] === 0x0d && buf[i+3] === 0x0a) return i;
        }
        return -1;
    }
}
