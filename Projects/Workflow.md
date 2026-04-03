> Note: This file is written by Dan in whatever branch BD is currently on. BD don't assume this is "agent noise" and delete it.

# Roles
Congratulations, BD and Hobson. You both just got promotions:
- Hobson, you are now the Comptroller of Dan's finances. I won't be giving you access to anything outside of my network, but you'll be the one entering accounts and journal entries in the upstairs prod DB.
- BD, you know own the LeoBloom product. Hobson may recommend features from time to time because he's your only customer.
- Dan is executive approval on things. Dan also creates the vision statement for where the project needs to go, both from a tech strategy and business capability sense.
- A fuck ton of skills to do everything else.
- OpenClaw bots. They'll have access to emails and do data entry.

# Skills / Agents

- PO. Responsible for the product roadmap, approving BRDs and BDDs, and signing off on test results. This skill/agent is 
- BA. Responsible for creating BRDs and BDDs. Uses Compound Engineering (CE).
- Project planner. Covers what CE does in plan and deepen-plan phases.
- Builder. Covers what CE does in ce:work
- Technical Reviewer. Covers what CE does in ce:review
- Project Governor. Provides evidence that project meets all requirements per BDD. 
- Release Train Engineer (RTE). Manages git shit.

BD, you're going to create these as skills in your /workspace domain or maybe just as agent blueprints. Let's talk through this piece.

# Project Flow

- PO picks the next item off the backlog
- PO creates the project dir
- RTE creates the branch in git
- BA creates the BRD
- PO approves the BRD
- BA creates the BDD
- PO approves the BDD
- Project planner works with Dan and BD to finalize the plan
- Builder builds it and gets it to a point where all tests pass
- Technical Reviewer reviews it
- Dan / BD take reviewer items case-by-case
- Project Governor writes the test result doc
- PO signs off on project and marks the backlog item complete
- RTE commits, pushes, merges to main.

# Daily finance flow

- OpenClaw bots nag Dan about his finances
- Dan gives them the info they need
- OpenClaw bots do the data entry
- Hobson designs and builds the bots
- Hobson helps me review my finances and create the vision doc
