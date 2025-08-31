{ git Tips&Tricks

how to see all branches even copy of remote ones {

$ git branch -a

* main                  our branch

  remotes/origin/main  copy of remote branch - updated with git fetch or git push/pull

  }

how to see local changes before we commit them {

git diff (no parameters) Print out differences between your working directory and the index(stage) .

git diff --cached                               Print out differences between the index and HEAD (current commit).

                                                                It will be different only after fetch or add

git diff HEAD                                     Print out differences between your working directory and the HEAD.

git diff HEAD origin/main            Print out differences between HEAD and remote HEAD.

git diff origin/main                         Print out differences between work dir and remote HEAD.

--- diff options ---

git diff --name-only                        Show only names of changed files.

git diff --name-status                   Show only names and status of changed files.

git diff --color-words                     Word by word diff instead of line by line.

git diff --word-diff                           Same

--- show commits ---

git show                                               Show last commit

git show HEAD                                 Same

git show commit-id                       Show any commit

git show --word-diff                       Show last commit with word-diff instead of line-diff

 

work_dir/your_files       index/stage                       HEAD/local_repo_copy              remote_HEAD/origin/remote_repo

    --------- add --------->                                                                                                                                

    ------ git diff  ------>                                                                                                                                    

                                                    -------- commit --------->                                                                                         

                                                    --- git diff --cached --->                                                                                           

    ----------------- git diff HEAD ----------------->                                                                                   

                                                                                                     ------------- push -------------->                              

                                                                                                     <------------ fetch --------------               

                                                                                                     --- git diff HEAD origin/main --->                        

    <------------------------------------- pull ---------------------------------------                

    ------------------------------ git diff origin/main ------------------------------>                            

}

how to see remote changes before we pull them {

git fetch

git diff HEAD origin

git log origin/main          -> all comits on origin

git log HEAD..origin/main           -> only commits not yet applied

- git pull = git fetch + git merge - git fetch updates local remote origin copy, doesn;t touch working dir

  }

how to see last commit - git show

how to show any commit - git show commit_id

how to commit {

git fetch

git status

git diff HEAD origin (/main)

git pull "or" git merge

git status

git add .

git commit

git push

  }

how to connect to gitlab {

# create key pair

head .ssh/config

# GitLab

Host qnbgitlab.qnb.bnk qnbgitlab

        User git

        IdentityFile ~/.ssh/id_git

# test

ssh qnbgitlab

# should return

Welcome to GitLab, @PQA7901!

#

}

} Git Tips&Tricks
