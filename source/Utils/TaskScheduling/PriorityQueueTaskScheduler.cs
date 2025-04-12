using Open.Collections.Synchronized;
using System.Diagnostics.Contracts;

namespace SolvePi.Utils.TaskScheduling;

public sealed class PriorityQueueTaskScheduler : DisposableTaskScheduler
{
	/// <inheritdoc />
	/// <summary>Initializes the scheduler.</summary>
	/// <param name="parent">The target underlying scheduler onto which this sceduler's work is queued.</param>
	/// <param name="maxConcurrencyLevel">The maximum degree of concurrency allowed for this scheduler's work.</param>
	public PriorityQueueTaskScheduler(
		TaskScheduler? parent = null,
		int maxConcurrencyLevel = 0)
	{
		_parent = parent ?? Current;
		ArgumentOutOfRangeException.ThrowIfNegative(maxConcurrencyLevel);
		Contract.EndContractBlock();

		// Make sure whatever value we pick is not greater than the degree of parallelism allowed by the underlying scheduler.
		if (maxConcurrencyLevel == 0)
			maxConcurrencyLevel = _parent.MaximumConcurrencyLevel;

		if (_parent.MaximumConcurrencyLevel > 0
			&& _parent.MaximumConcurrencyLevel < maxConcurrencyLevel)
		{
			maxConcurrencyLevel = _parent.MaximumConcurrencyLevel;
		}

		MaximumConcurrencyLevel = maxConcurrencyLevel;
	}

	/// <summary>
	/// Reverse priority causes the child queues to be queried in reverse order.
	/// So if set to true, the last child scheduler will be queried first.
	/// </summary>
	// ReSharper disable once UnusedAutoPropertyAccessor.Global
	public bool ReversePriority { get; set; }
	public string? Name { get; set; } // Useful for debugging.
	private readonly TaskScheduler _parent;

	private int _delegatesQueuedOrRunning;
	private readonly ConcurrentQueue<Task?> _internalQueue = new();

#if DEBUG
	protected override bool TryDequeue(Task task)
	{
		Debug.Fail("TryDequeue should never be called");
		return base.TryDequeue(task);
		//if (task is null) throw new ArgumentNullException(nameof(task));
		//Contract.EndContractBlock();

		//return InternalQueue.Remove(task);
	}
#endif

	private bool TryGetNext(out (PriorityQueueTaskScheduler scheduler, Task task) entry)
	{
		//Debug.WriteLineIf(Name is not null && Name.StartsWith("Level"), $"{Name} ({Id}): TryGetNext(out entry)");

		entry = default;
		if (DisposeCancellation.IsCancellationRequested)
			return false;

		if (!_internalQueue.TryDequeue(out Task? task))
			return false;

		if (task is not null)
		{
			entry = (this, task);
			return true;
		}

		bool rp = ReversePriority;
		LinkedListNode<PriorityQueueTaskScheduler>? node = rp ? _children.Last : _children.First;
		while (node is not null)
		{
			if (node.Value.TryGetNext(out entry))
				return true;

			node = rp ? node.Previous : node.Next;
		}

		return false;
	}

	private readonly ReadWriteSynchronizedLinkedList<PriorityQueueTaskScheduler> _children = [];

	private void NotifyNewWorkItem()
		// ReSharper disable once AssignNullToNotNullAttribute
		=> QueueTask(null);

	public PriorityQueueTaskScheduler this[int index]
	{
		get
		{
			if (index < 0)
				throw new ArgumentOutOfRangeException(nameof(index), index, "Must be at least zero");
			Contract.EndContractBlock();

			LinkedListNode<PriorityQueueTaskScheduler>? node = _children.First;
			if (node is null)
			{
				_children.Modify(
					() => _children.First is null,
					list => list.AddLast(new PriorityQueueTaskScheduler(this)));
				node = _children.First;
			}

			Debug.Assert(node is not null);

			for (int i = 0; i < index; i++)
			{
				if (node.Next is null)
				{
					LinkedListNode<PriorityQueueTaskScheduler> n = node;
					_children.Modify(
						() => n.Next is null,
						list => list.AddLast(new PriorityQueueTaskScheduler(this)));
				}

				node = node.Next;

				Debug.Assert(node is not null);
			}

			return node.Value;
		}
	}

	/// <inheritdoc />
	public override int MaximumConcurrencyLevel { get; }

	/// <inheritdoc />
	protected override IEnumerable<Task> GetScheduledTasks()
		=> _internalQueue
			.Where(e => e is not null)
			.ToList()!;

	private void ProcessQueues()
	{
		bool continueProcessing = true;
		while (continueProcessing && !DisposeCancellation.IsCancellationRequested)
		{
			try
			{
				while (TryGetNext(out (PriorityQueueTaskScheduler scheduler, Task task) entry))
				{
					(PriorityQueueTaskScheduler scheduler, Task task) = entry;
					Debug.Assert(scheduler is not null);
					Debug.Assert(task is not null);

					_ = scheduler.TryExecuteTask(task);
				}
			}
			finally
			{
				// ReSharper disable once InvertIf
				if (_internalQueue.IsEmpty)
				{
					// Now that we think we're done, verify that there really is
					// no more work to do.  If there's not, highlight
					// that we're now less parallel than we were a moment ago.
					lock (_internalQueue)
					{
						// ReSharper disable once InvertIf
						if (_internalQueue.IsEmpty)
						{
							--_delegatesQueuedOrRunning;
							continueProcessing = false;
						}
					}
				}
			}
		}
	}

	/// <inheritdoc />
	protected override void QueueTask(Task? task)
	{
		DisposeCancellation.Token.ThrowIfCancellationRequested();

		_internalQueue.Enqueue(task);
		//Debug.WriteLineIf(Name is not null && task is not null, $"{Name} ({Id}): QueueTask({task?.Id})");

		if (_parent is PriorityQueueTaskScheduler p)
		{
			p.NotifyNewWorkItem();
			return;
		}

		if (_delegatesQueuedOrRunning >= MaximumConcurrencyLevel)
			return;

		lock (_internalQueue)
		{
			if (_delegatesQueuedOrRunning >= MaximumConcurrencyLevel)
				return;

			++_delegatesQueuedOrRunning;
		}

		Task.Factory.StartNew(ProcessQueues,
			CancellationToken.None, TaskCreationOptions.None, _parent);
	}

	protected override void OnDispose() => _children.Dispose();
}
