- look into "active" harmony patches and see if we can refactor them into data structures which contain a lambda to run and contain a description (short and long maybe?) so that we can potentially have a UI which can manage view the registered active patches, their ordering, and manage them.  the list of active patches should also be maintained in a holding object that the harmony patch class simply has a reference to, that way other mod code can maintain the state of active patches

- refactor garys-torch to be garrys-torch


