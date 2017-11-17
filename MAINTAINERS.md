The current maintainers of this repo are [aws-maintainers](https://github.com/orgs/Particular/teams/aws-maintainers).

The maintainers [watch](https://github.com/Particular/NServiceBus.AmazonSQS/watchers) this repo and undertake the following responsibilities:

- Ensuring that the `master` and `support-x.y` branches are always releasable. "Releasable" means that the software built from the latest commit contains no known regressions from the previous release and can be released immediately.
  - This does not imply that the latest commit should *always* be released, only that it *can* be, immediately after deciding to release.
- Releasing new versions of the software.
- Reviewing and merging pull requests](https://github.com/Particular/NServiceBus.AmazonSQS/pulls).
- [Issue backlog](https://github.com/Particular/NServiceBus.AmazonSQS/issues) grooming, including the triage of new issues as soon as possible after they are created.
- Managing the repo settings (options, collaborators & teams, branches, etc.).

## Merging pull requests

### Summarized workflow for merging pull requests:

  1. Reviewer: Request changes or comment.
  2. Submitter: Push fixup.
  3. (Repeat)
  4. Reviewer: Request squash.
  5. Submitter: Squash and force push.
  6. Reviewer: Approve.
  7. (Any maintainer): Merge.

### Details 

- A pull request must be approved by two maintainers before it is merged.
  - A pull request created by a maintainer is implicitly approved by that maintainer.
  - Before approving, the maintainer should consider whether a smoke test is required.
  - Approval is given by submitting a review and choosing the **Approve** option:
  - For some pull requests, it may be appropriate to require a third maintainer to give approval before the pull request is merged. This may be requested by either of the current approvers based on their assessment of factors such as the impact or risk of the changes.
- An approved pull request may be merged by any maintainer.

### Branching and workflow

This repository follows the [GitFlow](http://nvie.com/posts/a-successful-git-branching-model/) branching model and workflow.
